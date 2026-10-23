using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace LectureLens.Desktop.Adapters;
internal static class KeyVault
{
 internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LectureLens","settings");
 private static string KeyPath(bool speech)=>Path.Combine(Folder,speech?"groq-key.dpapi":"nvidia-key.dpapi");
 internal static bool HasKey=>File.Exists(KeyPath(false));
 internal static bool HasSpeechKey=>File.Exists(KeyPath(true));
 internal static void Save(string key,bool speech=false)
 {
  key=key.Trim();var path=KeyPath(speech);
  if(!key.StartsWith(speech?"gsk_":"nvapi-")||key.Length<20)throw new InvalidDataException("Invalid API key");
  Directory.CreateDirectory(Folder);
  var clear=Encoding.Unicode.GetBytes(key.Trim());
  try{var encrypted=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser);File.WriteAllText(path+".partial",Convert.ToHexString(encrypted));File.Move(path+".partial",path,true);}finally{CryptographicOperations.ZeroMemory(clear);}
 }
 internal static string Read(bool speech=false)
 {
  var bytes=ProtectedData.Unprotect(Convert.FromHexString(File.ReadAllText(KeyPath(speech)).Trim()),null,DataProtectionScope.CurrentUser);
  try{return Encoding.Unicode.GetString(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
 }
}
