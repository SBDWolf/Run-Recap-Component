# Run-Recap-Component
A Livesplit Component for interaction with [Run Recap](https://myekul.com/run-recap/)

Uses some code from [DevilSquirrel's autosplitter](https://github.com/ShootMe/LiveSplit.Cuphead), mainly with memory-reading.

# What this does
This component will read Cuphead's memory and Livesplit's state to generate more granular data about a speedrun to a .rrc file, which can then be fed to [Run Recap](https://myekul.com/run-recap/) for post-run analysis.
Here's what's currently being stored to the file:
* The value of the Loadless Timer after each loading screen, which allows analysis on overworld movement, intermissions, cutscenes, and scoreboards
* In-Game Level Time for every completed level
* Values loaded on the scoreboard (HP Bonus, Parries, Super Meter, Coin)
* Final Time for the run


# Installation Instructions
* Grab the [latest release](https://github.com/SBDWolf/Run-Recap-Component/releases) (`RunRecap.zip`, NOT the Source code!)
* Extract all its contents into Livesplits's Components folder
* Launch Livesplit, then add the Cuphead Run Recap component to your Layout (Right click on the Livesplit window, select Edit Layout, click on the Plus sign, then you should find this component under "Control"
* By default, the .rrc file will be generated inside of `./Components/Run Recap`. This can be changed within your layout settings
