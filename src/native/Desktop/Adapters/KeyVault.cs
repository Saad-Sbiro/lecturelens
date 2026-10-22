using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace LectureLens.Desktop.Adapters;
internal static class KeyVault
{
 internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LectureLens","settings");
 private static string KeyPath=>Path.Combine(Folder,"nvidia-key.dpapi");
 internal static bool HasKey=>File.Exists(KeyPath);
 internal static void Save(string key)
 {
  if(!key.StartsWith("nvapi-")||key.Length<20)throw new InvalidDataException("Invalid API key");
  Directory.CreateDirectory(Folder);
  var clear=Encoding.Unicode.GetBytes(key.Trim());
  try{var encrypted=ProtectedData.Protect(clear,null,DataProtectionScope.CurrentUser);File.WriteAllText(KeyPath+".partial",Convert.ToHexString(encrypted));File.Move(KeyPath+".partial",KeyPath,true);}finally{CryptographicOperations.ZeroMemory(clear);}
 }
 internal static string Read()
 {
  var bytes=ProtectedData.Unprotect(Convert.FromHexString(File.ReadAllText(KeyPath).Trim()),null,DataProtectionScope.CurrentUser);
  try{return Encoding.Unicode.GetString(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
 }
}
