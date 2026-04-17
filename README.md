# AutoMinion

AutoMinion is a Dalamud plugin that automatically changes your minion when you switch to a specific job.

## Current Features

- Lists all owned minions from the companion sheet, sorted alphabetically based on client language
- Searchable dropdown to quickly filter minions (case-insensitive)
- Assign a specific minion per job
- Automatically summons the assigned minion on job change
- Avoids attempting to summon in restricted states (e.g. mounted, using an umbrella, or certain instanced content)
- Queues a retry when summoning becomes available again

## Install and Usage

1. Go to `/xlsettings` and navigate to the **Experimental** tab
2. Add the following custom plugin repository:
    `https://raw.githubusercontent.com/seanf26/AutoMinion/main/pluginmaster.json`
3. Click the `+` button, then **Save and Close**
4. Open the Dalamud Plugin Installer and search for **AutoMinion**
5. Open the AutoMinion configuration via plugin settings or `/autominion`
6. Add a job row and select a minion for that job

## Notes

- Minions are only changed when switching jobs, not continuously
- The plugin will not override game restrictions on summoning
- If a summon fails due to game state, it will retry shortly after

## Disclaimer

This was built primarily for personal use to avoid maintaining macros for each job.

It should work for others, but there may be issues. Feel free to report them and I will do what I can to help, but I am a hobbyist and my programming skills are mediocre.