using System.ComponentModel;
using System.Windows.Forms;
using WinSCP;

namespace Arma3DedicatedServerSyncer
{
    public static class Extensions
    {
        public static void InvokeIfRequired(this ISynchronizeInvoke obj,
                                         MethodInvoker action)
        {
            if (obj.InvokeRequired)
            {
                var args = new object[0];
                obj.Invoke(action, args);
            }
            else
            {
                action();
            }
        }

        public static string TranslateLocalPathToRemote(this Session session, string localFileFullName, string localFullPath, string remotepath, string addon)
        {
            string result = "";

            //result = localFileFullName + "|" + localFullPath + "|" + remotepath;
            result = remotepath + "/" + addon + localFileFullName.Replace(localFullPath, "").Replace("\\","/");

            return result;
        }

    }
}
