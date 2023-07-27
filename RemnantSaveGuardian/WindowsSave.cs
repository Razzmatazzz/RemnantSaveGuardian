using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemnantSaveGuardian
{
    class WindowsSave
    {
        public string Container { get; set; }
        public string Profile { get; set; }
        public List<string> Worlds { get; set; }
        private bool isValid;
        public bool Valid { get { return isValid; } }

        public WindowsSave(string containerPath)
        {
            Worlds = new List<string>();
            Container = containerPath;
            var folderPath = new FileInfo(containerPath).Directory.FullName;
            var offset = 136;
            byte[] byteBuffer = File.ReadAllBytes(Container);
            var profileBytes = new byte[16];
            Array.Copy(byteBuffer, offset, profileBytes, 0, 16);
            var profileGuid = new Guid(profileBytes);
            Profile = profileGuid.ToString().ToUpper().Replace("-", "");
            isValid = File.Exists($@"{folderPath}\{Profile}");
            offset += 160;
            while (offset + 16 < byteBuffer.Length)
            {
                var worldBytes = new byte[16];
                Array.Copy(byteBuffer, offset, worldBytes, 0, 16);
                var worldGuid = new Guid(worldBytes);
                Worlds.Add(folderPath + "\\" + worldGuid.ToString().ToUpper().Replace("-", ""));
                offset += 160;
            }
        }
    }
}
