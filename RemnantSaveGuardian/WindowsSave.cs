using System;
using System.Collections.Generic;
using System.IO;

namespace RemnantSaveGuardian
{
    class WindowsSave
    {
        public string Container { get; set; }
        public string Profile { get; set; }
        public List<string> Worlds { get; set; }
        private readonly bool _isValid;
        public bool Valid => _isValid;

        public WindowsSave(string containerPath)
        {
            Worlds = new List<string>();
            Container = containerPath;
            string folderPath = new FileInfo(containerPath).Directory.FullName;
            int offset = 136;
            byte[] byteBuffer = File.ReadAllBytes(Container);
            byte[] profileBytes = new byte[16];
            Array.Copy(byteBuffer, offset, profileBytes, 0, 16);
            Guid profileGuid = new(profileBytes);
            Profile = profileGuid.ToString().ToUpper().Replace("-", "");
            _isValid = File.Exists($@"{folderPath}\{Profile}");
            offset += 160;
            while (offset + 16 < byteBuffer.Length)
            {
                byte[] worldBytes = new byte[16];
                Array.Copy(byteBuffer, offset, worldBytes, 0, 16);
                Guid worldGuid = new(worldBytes);
                Worlds.Add(folderPath + "\\" + worldGuid.ToString().ToUpper().Replace("-", ""));
                offset += 160;
            }
        }
    }
}
