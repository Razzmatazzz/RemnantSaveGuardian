# Remnant 2 Save Analyzer

This is a fork of the excellent [Remnant Save Guardian](https://github.com/Razzmatazzz/RemnantSaveGuardian) originally created by Razzmatazz.

## Differences with Remnant Save Guardian

- The underlying save parsing engine is completely different and allows for more fidelity in item detecton
- Every single item showed by the program has a tooltip that describes how to find it
- Hide Ward 13, Labyrinth and Root Earth, once you feel you know them by heart
- Hide Tomes of Knowledge, once you are maxed out, you might not be that much interested in them
- See connections between locations
- See World Stones
- See if a Simulacrum is present in a location
- Export save as json for geeks only


## Potential improvements

This is a list of things that are possible to add with the new engine, but which requrie some additiona work

- See items offered in Cass shop
- Track objectives progression, e.g. kill x bosses

## Known issues

Most of these issues are unlikely to be fixed, unless someone would like to help:

- This program is a bit slower than RSG due to different parsing engine. On SSD the slowdown is noticable but IMO tolerable
- Translations might not work as they used, and for the newer content there are none
- I'm planning on improving this one, if I have the time: "Difficult" items are not working yet: Amulet_GoldenRibbon, Amulet_GunfireSecurityLanyard, Amulet_ParticipationMedal, Amulet_SilverRibbon, Armor_Body_CrimsonGuard, Engram_Archon, Relic_Consumable_QuiltedHeart, Relic_Consumable_RipenedHeart, Ring_BandOfTheFanatic, Ring_BisectedRing, Ring_DowngradedRing, Void_Heart, Weapon_Anguish, Weapon_CrescentMoon
- [If you are using Norton Antivirus, it may cause weirdness with your game saves and RemnantSaveGuardian](https://github.com/Razzmatazzz/RemnantSaveGuardian/issues/70)

## Save Parsing

These are the save parsing libraries being used underneath:
- <https://github.com/AndrewSav/lib.remnant2.saves> - This parses the save files to an object model
- <https://github.com/AndrewSav/lib.remnant2.analyzer> - Uses the above to get information about items from the saves

*Note: The object model for and Unreal Engine game save is incredibly complicated, it is not mean for usage out of the game, the way we do it here. It can lead to a lot of subtle usage issue, but it is much better than the original string parsing*

## Acknowledgement

Thanks to all original project contributors!
