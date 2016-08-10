using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Arma3DedicatedServerSyncer
{
    [Serializable]
    public class Settings
    {
        [XmlElement(ElementName = "Version")]
        public string AppVersion { get; set; }

        [XmlElement(ElementName = "LocalPath")]
        public string Arma3LocalPath { get; set; }
        
        
        
        public string RemoteHost { get; set; }

        public string RemotePort { get; set; }

        public string RemoteLogin { get; set; }

        public string RemotePassword { get; set; }

        public string RemoteBaseDirectory { get; set; }

        public string RemoteKeyFingerprint { get; set; }


    }
}
