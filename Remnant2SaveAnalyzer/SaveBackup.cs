using lib.remnant2.analyzer;
using Remnant2SaveAnalyzer.Logging;
using System;
using System.ComponentModel;
using System.IO;

namespace Remnant2SaveAnalyzer
{
    public class SaveBackup : IEditableObject
    {
        struct SaveData
        {
            internal string Name;
            internal DateTime Date;
            internal bool Keep;
            internal bool Active;
        }

        public event EventHandler<UpdatedEventArgs>? Updated;
        private SaveData _saveData;
        private SaveData _backupData;
        private bool _inTxn;
        //private int[] progression;
        //private List<RemnantCharacter> charData;
        private readonly string _savePath;

        public string Name
        {
            get => _saveData.Name;
            set => _saveData.Name = value.Equals("") ? _saveData.Date.Ticks.ToString() : value;
            //OnUpdated(new UpdatedEventArgs("Name"));
        }
        public DateTime SaveDate
        {
            get => _saveData.Date;
            set => _saveData.Date = value;
            //OnUpdated(new UpdatedEventArgs("SaveDate"));
        }
        public string? Progression { get; }

        public bool Keep
        {
            get => _saveData.Keep;
            set
            {
                if (_saveData.Keep != value)
                {
                    _saveData.Keep = value;
                    OnUpdated(new UpdatedEventArgs("Keep"));
                }
            }
        }
        public bool Active
        {
            get => _saveData.Active;
            set => _saveData.Active = value;
            //OnUpdated(new UpdatedEventArgs("Active"));
        }

        public string SaveFolderPath => _savePath;

        //public SaveBackup(DateTime saveDate)
        public SaveBackup(string savePath)
        {
            _savePath = savePath;

            try
            {
                Progression = Analyzer.GetProfileStringCombined(_savePath);
            }
            catch (Exception ex)
            {
                Notifications.Error($"Could not read profile from backup: {ex}");
            }

            _saveData = new SaveData
            {
                Name = SaveDateTime.Ticks.ToString(),
                Date = SaveDateTime,
                Keep = false
            };
        }

        /*public void setProgression(List<List<string>> allItemList)
        {

            int[] progression = new int[allItemList.Count];
            for (int i=0; i < allItemList.Count; i++)
            {
                progression[i] = allItemList[i].Count;
            }
            this.progression = progression;
        }
        public List<RemnantCharacter> GetCharacters()
        {
            return this.charData;
        }
        public void LoadCharacterData(string saveFolder)
        {
            this.charData = RemnantCharacter.GetCharactersFromSave(saveFolder, RemnantCharacter.CharacterProcessingMode.NoEvents);
        }*/

        // Implements IEditableObject
        void IEditableObject.BeginEdit()
        {
            if (!_inTxn)
            {
                _backupData = _saveData;
                _inTxn = true;
            }
        }

        void IEditableObject.CancelEdit()
        {
            if (_inTxn)
            {
                _saveData = _backupData;
                _inTxn = false;
            }
        }

        void IEditableObject.EndEdit()
        {
            if (_inTxn)
            {
                if (!_backupData.Name.Equals(_saveData.Name))
                {
                    OnUpdated(new UpdatedEventArgs("Name"));
                }
                if (!_backupData.Date.Equals(_saveData.Date))
                {
                    OnUpdated(new UpdatedEventArgs("SaveDate"));
                }
                if (!_backupData.Keep.Equals(_saveData.Keep))
                {
                    OnUpdated(new UpdatedEventArgs("Keep"));
                }
                if (!_backupData.Active.Equals(_saveData.Active))
                {
                    OnUpdated(new UpdatedEventArgs("Active"));
                }
                _backupData = new SaveData();
                _inTxn = false;
            }
        }

        public void OnUpdated(UpdatedEventArgs args)
        {
            Updated?.Invoke(this, args);
        }

        private DateTime SaveDateTime => File.GetLastWriteTime(Path.Join(_savePath, "profile.sav"));
    }

    public class UpdatedEventArgs(string fieldName) : EventArgs
    {
        public string FieldName { get; } = fieldName;
    }
}
