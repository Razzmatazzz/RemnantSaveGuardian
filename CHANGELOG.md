# Changelog

## 0.0.6 (unreleased)
- Add ability to hide items if they cannot be achived in the current save due to missing prerequisites - work in progress

## 0.0.5 (8 May 2024)
- Fixed release zip file, it wrongly used to have the release in the "publish" subfolder
- Dim items in "Possile Items" columns if these items were already looted in the save if possible. This does not work for all items, as not all items appear in saves. Also added an option not to dim
- Also added an option to completely hide the looted items above in the World Analyzer


## 0.0.4 (6 May 2024)
- Add option to log save data for debugging purposes
- Add option to dump analyzer data structures in json for debugging purposes
- Add option to log performance metrics for debugging purposes
- Fixed a few typos

## 0.0.3 (3 May 2024)

- Improve filtering out DLC items (in case you do not own DLC and do not want them shown)
- Fixed an issue where localisation to Location column in World Analyser should be applied and it was not
- Fixed potential crash on backup save [Razzmatazzz#228](https://github.com/Razzmatazzz/RemnantSaveGuardian/issues/228) [Razzmatazzz#208](https://github.com/Razzmatazzz/RemnantSaveGuardian/issues/208)

## 0.0.2 (3 May 2024)

- Initial release
