# DutyDM bot

The server half of DutyDM. The plugin POSTs `{ username, title, message, secret }` to
`https://marobotic.dev/dutydm/notify` and this bot DMs that Discord user (found by @username among
the members of the DutyDM server).

Runs on the marobotic.dev droplet (157.245.131.195): `/opt/dutydm-bot`, pm2 app `dutydm-bot`,
`/opt/node22/bin/node`, listening on `127.0.0.1:1502`; nginx proxies `/dutydm/` to it.
The first version lived only on the old 138.68.142.140 box and was lost with it - this is the rebuild.

## Setup

1. Discord Developer Portal > application 1468337866227580969 > Bot: turn on
   **Server Members Intent** (it searches members by username), copy the token.
2. `/opt/dutydm-bot/.env` (mode 600, never committed):

   ```
   DISCORD_TOKEN=...
   GUILD_ID=          # optional: only the DutyDM server
   PUSH_SECRET=       # optional: must equal BotEndpoint.Secret in the plugin
   ```

3. `npm ci --omit=dev` then `pm2 start bot.js --name dutydm-bot --interpreter /opt/node22/bin/node && pm2 save`.

Check: `curl https://marobotic.dev/dutydm/health` -> `{"ok":true,"bot":"..."}`.
