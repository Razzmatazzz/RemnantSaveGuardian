using System;
using System.ComponentModel;
using System.IO;
using lib.remnant2.analyzer;

namespace RemnantSaveGuardian
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

        public event EventHandler<UpdatedEventArgs> Updated;
        private SaveData _saveData;
        private SaveData _backupData;
        private bool _inTxn = false;
        //private int[] progression;
        //private List<RemnantCharacter> charData;
        private string _progression;
        private string _savePath;

        public string Name
        {
            get
            {
                return _saveData.Name;
            }
            set
            {
                if (value.Equals(""))
                {
                    _saveData.Name = _saveData.Date.Ticks.ToString();
                }
                else
                {
                    _saveData.Name = value;
                }
                //OnUpdated(new UpdatedEventArgs("Name"));
            }
        }
        public DateTime SaveDate
        {
            get
            {
                return _saveData.Date;
            }
            set
            {
                _saveData.Date = value;
                //OnUpdated(new UpdatedEventArgs("SaveDate"));
            }
        }
        public string Progression
        {
            get
            {
                return string.Join(", ", _progression);
            }
        }
        public bool Keep
        {
            get
            {
                return _saveData.Keep;
            }
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
            get
            {
                return _saveData.Active;
            }
            set
            {
                _saveData.Active = value;
                //OnUpdated(new UpdatedEventArgs("Active"));
            }
        }

        public string SaveFolderPath
        {
            get
            {
                return _savePath;
            }
        }

        //public SaveBackup(DateTime saveDate)
        public SaveBackup(string savePath)
        {
            _savePath = savePath;

            _progression = Analyzer.GetProfileStringCombined(_savePath);
            _saveData = new SaveData();
            _saveData.Name = SaveDateTime.Ticks.ToString();
            _saveData.Date = SaveDateTime;
            _saveData.Keep = false;
        }

        /*public void setProgression(List<List<string>> allItemList)
        {

            int[] prog = new int[allItemList.Count];
            for (int i=0; i < allItemList.Count; i++)
            {
                prog[i] = allItemList[i].Count;
            }
            this.progression = prog;
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
            EventHandler<UpdatedEventArgs> handler = Updated;
            if (null != handler) handler(this, args);
        }

        private DateTime SaveDateTime
        {
            get
            {
                return File.GetLastWriteTime(Path.Join(_savePath, "profile.sav"));
            }
        }
    }

    public class UpdatedEventArgs : EventArgs
    {
        private readonly string _fieldName;

        public UpdatedEventArgs(string fieldName)
        {
            _fieldName = fieldName;
        }

        public string FieldName
        {
            get { return _fieldName; }
        }
    }
}
