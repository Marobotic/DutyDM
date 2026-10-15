// DutyDM bot: the plugin POSTs { username, title, message, secret } to /notify and this DMs that
// Discord user. The user is found by their unique @username among the members of the server(s) the
// bot is in, so they must have joined the DutyDM server first.
//
// Env (from .env next to this file, or the process environment):
//   DISCORD_TOKEN  the bot token (Developer Portal > Bot). Required.
//   GUILD_ID       only look users up in this server. Optional; default: every server the bot is in.
//   PUSH_SECRET    must match BotEndpoint.Secret in the plugin. Optional; empty = no secret.
//   HOST, PORT     where /notify listens. Default 127.0.0.1:1502 (nginx proxies /dutydm/ to it).
//
// Replies the plugin understands (DiscordBotChannel.MapError): 200 ok, 400 missing_username,
// 401 bad secret, 404 user_not_found, 502 dm_failed, 429 slow down, 503 still starting.
"use strict";
const fs = require("fs");
const http = require("http");
const path = require("path");
const { Client, GatewayIntentBits, EmbedBuilder, Events } = require("discord.js");

const envFile = path.join(__dirname, ".env");
if (fs.existsSync(envFile)) {
  for (const line of fs.readFileSync(envFile, "utf8").split(/\r?\n/)) {
    const m = line.match(/^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^["']|["']$/g, "");
  }
}

const TOKEN = process.env.DISCORD_TOKEN || "";
const GUILD_ID = process.env.GUILD_ID || "";
const SECRET = process.env.PUSH_SECRET || "";
const HOST = process.env.HOST || "127.0.0.1";
const PORT = Number(process.env.PORT || 1502);

const PER_USER_MS = 3000;       // one DM per username every 3 s; a duty pop is one call
const PER_ADDR = { n: 20, ms: 60000 };   // and 20 calls a minute from one address
const MAX_BODY = 8 * 1024;

if (!TOKEN) {
  console.error("DISCORD_TOKEN is not set (put it in bot/.env)");
  process.exit(1);
}

function log(...a) { console.log(new Date().toISOString(), ...a); }

// ---------------------------------------------------------------- Discord
const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMembers] });
let ready = false;
client.once(Events.ClientReady, (c) => {
  ready = true;
  log(`logged in as ${c.user.tag}, in ${c.guilds.cache.size} server(s):`,
      c.guilds.cache.map((g) => `${g.name} (${g.id})`).join(", "));
});
client.on(Events.ShardDisconnect, () => log("gateway disconnected"));
client.on(Events.ShardResume, () => log("gateway resumed"));
client.on(Events.Error, (e) => log("discord error:", e.message));

const userCache = new Map();    // lowercase username -> { id, at }
const CACHE_MS = 10 * 60 * 1000;

function cleanName(raw) {
  return String(raw || "").trim().replace(/^@/, "").replace(/#0$/, "").toLowerCase();
}

async function findUser(name) {
  const hit = userCache.get(name);
  if (hit && Date.now() - hit.at < CACHE_MS) return client.users.fetch(hit.id);
  const guilds = GUILD_ID ? [client.guilds.cache.get(GUILD_ID)].filter(Boolean) : [...client.guilds.cache.values()];
  for (const g of guilds) {
    // Search by prefix (needs the Server Members intent), then insist on an exact username match:
    // display names and nicknames are not unique, so a match on them could DM a stranger.
    const found = await g.members.fetch({ query: name, limit: 25 }).catch((e) => {
      log(`member search in ${g.name} failed:`, e.message);
      return null;
    });
    const member = found && found.find((m) => m.user.username.toLowerCase() === name);
    if (member) {
      userCache.set(name, { id: member.id, at: Date.now() });
      return member.user;
    }
  }
  return null;
}

// ---------------------------------------------------------------- HTTP
const lastPerUser = new Map();
const perAddr = new Map();

function limited(addr, name) {
  const now = Date.now();
  const a = (perAddr.get(addr) || []).filter((t) => now - t < PER_ADDR.ms);
  if (a.length >= PER_ADDR.n) return true;
  if (name && now - (lastPerUser.get(name) || 0) < PER_USER_MS) return true;
  a.push(now);
  perAddr.set(addr, a);
  if (name) lastPerUser.set(name, now);
  return false;
}
setInterval(() => {
  const now = Date.now();
  for (const [k, v] of perAddr) if (!v.some((t) => now - t < PER_ADDR.ms)) perAddr.delete(k);
  for (const [k, t] of lastPerUser) if (now - t > PER_USER_MS) lastPerUser.delete(k);
}, 60000).unref();

function send(res, status, body) {
  res.writeHead(status, { "Content-Type": "application/json" });
  res.end(JSON.stringify(body));
}

function clip(s, n) {
  s = String(s || "");
  return s.length > n ? s.slice(0, n - 1) + "…" : s;
}

async function notify(req, res) {
  let raw = "";
  for await (const chunk of req) {
    raw += chunk;
    if (raw.length > MAX_BODY) return send(res, 413, { error: "too_large" });
  }
  let body;
  try { body = JSON.parse(raw || "{}"); } catch { return send(res, 400, { error: "bad_json" }); }

  if (SECRET && body.secret !== SECRET) return send(res, 401, { error: "bad_secret" });
  const name = cleanName(body.username);
  if (!name) return send(res, 400, { error: "missing_username" });
  const addr = req.headers["x-real-ip"] || req.socket.remoteAddress || "";
  if (limited(addr, name)) return send(res, 429, { error: "rate_limited" });
  if (!ready) return send(res, 503, { error: "starting" });

  const user = await findUser(name);
  if (!user) {
    log(`notify ${name}: user_not_found`);
    return send(res, 404, { error: "user_not_found" });
  }
  const embed = new EmbedBuilder()
    .setColor(0xf5a524)
    .setTitle(clip(body.title || "DutyDM", 256))
    .setDescription(clip(body.message || "", 4000) || null)
    .setFooter({ text: "DutyDM" })
    .setTimestamp(new Date());
  try {
    await user.send({ embeds: [embed] });
  } catch (e) {
    log(`notify ${name}: dm_failed (${e.message})`);
    return send(res, 502, { error: "dm_failed" });
  }
  log(`notify ${name}: sent "${clip(body.title, 60)}"`);
  send(res, 200, { ok: true });
}

const server = http.createServer((req, res) => {
  const url = (req.url || "/").split("?")[0].replace(/\/+$/, "") || "/";
  if (req.method === "POST" && url === "/notify") {
    notify(req, res).catch((e) => {
      log("notify error:", e);
      if (!res.headersSent) send(res, 500, { error: "internal" });
    });
  } else if (req.method === "GET" && (url === "/health" || url === "/")) {
    send(res, ready ? 200 : 503, { ok: ready, bot: ready ? client.user.tag : null });
  } else {
    send(res, 404, { error: "not_found" });
  }
});
server.headersTimeout = 10000;
server.requestTimeout = 15000;
server.listen(PORT, HOST, () => log(`listening on ${HOST}:${PORT}`));

client.login(TOKEN).catch((e) => {
  log("login failed:", e.message);
  process.exit(1);
});
