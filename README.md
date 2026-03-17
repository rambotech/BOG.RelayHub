# BOG.RelayHub

Author: John J Schultz

## Overview

MinApi written in C# for inter-application transfer. Consists of:

- Pull-only channels for dropping-off and picking-up objects associated to the channel.
  - FIFO Queue, with designated recipient.
  - Key/Value Store

Objects are persisted witin the file system and are recovered at a restart, and 
persist until removed from the channel.

See the INSTRUCTIONS.md file for project usage, examples and notes.

The ConsumerDemo project demonstrates a client application's typical usage.

## History
1.3.0 -- 03/28/2024
- Release version

1.2.2 -- 03/12/2024
- Misc updates and logging adjustment
- Add property group in csproj files to prevent analytics errors in build.

1.2.1 -- 03/12/2024
- Updates to config strategy

1.2.0 -- 03/08/2024
- Release version

1.1.5 -- 03/05/2024
- Add RelayHubConfig to appsettings.json; command-line for overrides.
- Fix 451 handling logic for bad token timeout.
- Add JSON serialization headers to common classes.
- Add tests for bad token timeout to demo.

1.1.4 -- 02/20/2024
- Fix bug with blocking logic for auth.

1.1.3 -- 02/20/2024
- Adds Executive token value and endpoints for executive functions.

1.1.2 -- 02/10/2024
- Fixes a bug with File Recovery at startup.

1.1.1 -- 02/07 2024
- First release

1.1.0 -- 02/04/2024
- Testing with ConsumerDemo
- Drop channel token and stas/metrics persistent.
- Add executive functions (bulk channel handling, shutdown)
- Endpoint name adjustments to deconlict meanings.

1.0.6 -- 02/04/2024
- TBD Add metrics for a channel
- TBD Add stats for a channel, stored as channel_stats.json, readable by client.
- Pre 1.1.0 release with executive functions.

1.0.5 -- 01/29/2024 -- Testing adjustments
- Demo app now tests multi-threaded calls to the RelayHub app.

1.0.4 -- 01/28/2024 -- Operational release version.
- Fix logic which would not create the reference file.
- Optimize collision-prevention logic for queue file naming.

1.0.3 -- 01/23/2024 -- Bug fix
- Fix logic which would not create the reference file.
- Optimize collision-prevention logic for queue file naming.

1.0.2 -- 01/19/2024 -- First operational release.
- Misc bug fixes in WebAPI and Client.  
- Disambiguate: Change term Owner to Channel.
- README.md adjustments.

1.0.1 -- 01/17/2024
- First working release

1.0.0 -- 01/12/2024
- Original development
