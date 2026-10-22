using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;
using Microsoft.Win32;
namespace LectureLens.Desktop;
internal sealed class NotesStudio(FrameworkElement view)
{
 record Preferences(string Detail,string Language,bool IncludeSlides,string Model,bool KeepRaw);
 readonly DataGrid sessions=(DataGrid)view.FindName("Sessions");
 readonly TextBlock status=(TextBlock)view.FindName("SessionMessage");
 readonly ComboBox detail=(ComboBox)view.FindName("Detail");
 readonly ComboBox language=(ComboBox)view.FindName("PdfLanguage");
 readonly CheckBox images=(CheckBox)view.FindName("IncludeSlides");
 readonly CheckBox keep=(CheckBox)view.FindName("KeepRaw");
 string model=NvidiaNotes.DefaultModel;
 CancellationTokenSource? running;
 internal static bool IsGenerating { get; private set; }
 string S(string key)=>(string)view.FindResource(key);
 string PrefPath=>Path.Combine(KeyVault.Folder,"notes-preferences.json");
 string? Folder=>sessions.SelectedItem is SessionInfo s&&s.State!="recording"&&s.Problems.Length==0?Path.Combine(LectureLens.Storage.LocalStore.DefaultRoot,s.Id):null;
 internal void Attach()
 {
  detail.ItemsSource=new[]{new Choice("brief",S("brief")),new Choice("standard",S("standard")),new Choice("detailed",S("detailed"))};detail.DisplayMemberPath="Label";detail.SelectedValuePath="Id";detail.SelectedValue="standard";
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  var choices=registry.RootElement.EnumerateObject().Where(x=>x.Value.GetProperty("enabled").GetBoolean()).Select(x=>new Choice(x.Name,x.Value.GetProperty("nativeName").GetString()!)).ToArray();
  language.ItemsSource=choices;language.DisplayMemberPath="Label";language.SelectedValuePath="Id";language.SelectedValue=registry.RootElement.EnumerateObject().First(x=>x.Value.GetProperty("defaultOutput").GetBoolean()).Name;
  if(File.Exists(PrefPath))try{var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PrefPath))!;if(new[]{"brief","standard","detailed"}.Contains(p.Detail))detail.SelectedValue=p.Detail;if(choices.Any(c=>c.Id==p.Language))language.SelectedValue=p.Language;images.IsChecked=p.IncludeSlides;model=p.Model;keep.IsChecked=p.KeepRaw;}catch(JsonException){}
  Button("SavePreferences").Click+=(_,_)=>{Save();status.Text=S("preferencesSaved");};
  Button("KeySetup").Click+=(_,_)=>Settings();
  Button("ImportTranscript").Click+=(_,_)=>Import();
  Button("GeneratePdf").Click+=async(_,_)=>await Generate();
  Button("CancelGeneration").Click+=(_,_)=>running?.Cancel();
  Button("CancelGeneration").IsEnabled=false;
  sessions.SelectionChanged+=(_,_)=>{((CheckBox)view.FindName("CloudConsent")).IsChecked=false;UpdateSource();};UpdateSource();
 }
 record Choice(string Id,string Label);
 Button Button(string name)=>(Button)view.FindName(name);
 NotesPreferences Options()=>new((string)detail.SelectedValue,(string)language.SelectedValue,images.IsChecked==true);
 void Save(){Directory.CreateDirectory(KeyVault.Folder);var o=Options();File.WriteAllText(PrefPath+".partial",JsonSerializer.Serialize(new Preferences(o.Detail,o.Language,o.IncludeSlides,model,keep.IsChecked==true)));File.Move(PrefPath+".partial",PrefPath,true);}
 void UpdateSource(){var text=(TextBlock)view.FindName("TranscriptStatus");var folder=Folder;text.Text=folder!=null&&File.Exists(Path.Combine(folder,"notes-transcript.json"))?S("transcriptReady"):S("transcriptMissing");}
 void Import()
 {
  try{
   var folder=Folder;if(folder==null){status.Text=S("chooseSession");return;}
   var dialog=new OpenFileDialog{Filter=S("sourceSelect"),Title=S("attachMismatch")};if(dialog.ShowDialog()!=true)return;
   var sources=ReadSources(dialog.FileName);Directory.CreateDirectory(folder);
   File.WriteAllText(Path.Combine(folder,"notes-transcript.json.partial"),JsonSerializer.Serialize(sources,NvidiaNotes.Json));File.Move(Path.Combine(folder,"notes-transcript.json.partial"),Path.Combine(folder,"notes-transcript.json"),true);UpdateSource();status.Text=S("transcriptReady");
  }catch(Exception e){status.Text=e.Message;}
 }
 internal static NoteSource[] ReadSources(string path)
 {
  if(new FileInfo(path).Length>2_000_000)throw new InvalidDataException("Transcript preview limit: 2 MB");
  var raw=File.ReadAllText(path);var texts=new List<(string Label,string Text)>();
  if(Path.GetExtension(path).Equals(".json",StringComparison.OrdinalIgnoreCase)){
   using var doc=JsonDocument.Parse(raw);foreach(var s in doc.RootElement.GetProperty("segments").EnumerateArray()){
    if(s.TryGetProperty("kept",out var kept)&&kept.ValueKind==JsonValueKind.False)continue;
    var label=s.TryGetProperty("start",out var start)?TimeSpan.FromSeconds(start.GetDouble()).ToString(@"hh\:mm\:ss"):"";
    texts.Add((label,s.GetProperty("text").GetString()??""));
   }
  }else texts.Add(("",raw));
  var output=new List<NoteSource>();foreach(var (label,text) in texts){for(var offset=0;offset<text.Length;offset+=2000){var part=text.Substring(offset,Math.Min(2000,text.Length-offset)).Trim();if(part.Length>0){var id="S"+(output.Count+1);output.Add(new(id,label.Length>0?id+" · "+label:id,part));}}}
  if(output.Count==0||output.Sum(s=>s.Text.Length)>250000)throw new InvalidDataException("Attach a non-empty transcript with at most 250,000 characters. No text was silently truncated.");return output.ToArray();
 }
 internal static List<NoteSource[]> Parts(IReadOnlyList<NoteSource> sources)
 {
  var result=new List<NoteSource[]>();var batch=new List<NoteSource>();int count=0;
  foreach(var s in sources){if(count+s.Text.Length>10000&&batch.Count>0){result.Add(batch.ToArray());batch.Clear();count=0;}batch.Add(s);count+=s.Text.Length;}if(batch.Count>0)result.Add(batch.ToArray());return result;
 }
 async Task Generate()
 {
  if(running!=null)return;
  var folder=Folder;if(folder==null){status.Text=S("chooseSession");return;}
  if(((CheckBox)view.FindName("CloudConsent")).IsChecked!=true){status.Text=S("consentNeeded");return;}
  if(!KeyVault.HasKey){Settings();return;}
  var sourcePath=Path.Combine(folder,"notes-transcript.json");if(!File.Exists(sourcePath)){status.Text=S("transcriptMissing");return;}
  running=new CancellationTokenSource();IsGenerating=true;SetEditing(false);Button("GeneratePdf").IsEnabled=false;Button("ImportTranscript").IsEnabled=false;Button("CancelGeneration").IsEnabled=true;sessions.IsEnabled=false;
  try{
   Save();var options=Options();var retain=keep.IsChecked==true;
   var sources=JsonSerializer.Deserialize<NoteSource[]>(await File.ReadAllTextAsync(sourcePath),NvidiaNotes.Json)!;
   var parts=Parts(sources);var generator=new NvidiaNotes(model,Path.Combine(folder,"notes-cache"));var outputs=new List<StudyNotes>();
   for(int i=0;i<parts.Count;i++){status.Text=S("progressPart")+" "+(i+1)+" / "+parts.Count;outputs.Add(await generator.Generate(parts[i],options,running.Token));}
   var notes=new StudyNotes(outputs[0].Title,string.Join("\n\n",outputs.Select(n=>n.Overview)),outputs.SelectMany(n=>n.Sections).ToArray());
   var destination=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LectureLens","exports",Path.GetFileName(folder),DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);Directory.CreateDirectory(destination);
   await File.WriteAllTextAsync(Path.Combine(destination,"notes.json"),JsonSerializer.Serialize(notes,NvidiaNotes.Json));
   await File.WriteAllTextAsync(Path.Combine(destination,"preferences.json"),JsonSerializer.Serialize(options));
   var pdf=await new BrowserPdf().Render(notes,sources,options,folder,destination,running.Token);status.Text=S("done")+" "+pdf;Process.Start(new ProcessStartInfo(pdf){UseShellExecute=true});
   if(!retain&&MessageBox.Show(S("retentionConfirm"),"LectureLens",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot).Delete(Path.GetFileName(folder));sessions.ItemsSource=new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot).List();}
  }catch(OperationCanceledException){status.Text=S("cancel");}catch(Exception e){status.Text=e.Message;}
  finally{running.Dispose();running=null;IsGenerating=false;SetEditing(true);((CheckBox)view.FindName("CloudConsent")).IsChecked=false;Button("GeneratePdf").IsEnabled=true;Button("ImportTranscript").IsEnabled=true;Button("CancelGeneration").IsEnabled=false;sessions.IsEnabled=true;}
 }
 void SetEditing(bool enabled)
 {
  foreach(var name in new[]{"Refresh","Recover","ViewSlides","Stop","Delete","SavePreferences","KeySetup"})Button(name).IsEnabled=enabled;
  detail.IsEnabled=enabled;language.IsEnabled=enabled;images.IsEnabled=enabled;keep.IsEnabled=enabled;
 }
 void Settings()
 {
  var window=new Window{Title=S("keyTitle"),Width=540,Height=340,Owner=Window.GetWindow(view),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};var stack=new StackPanel{Margin=new Thickness(24)};
  stack.Children.Add(new TextBlock{Text=S("keyLabel"),Margin=new Thickness(0,0,0,8)});var password=new PasswordBox{Padding=new Thickness(10)};stack.Children.Add(password);
  stack.Children.Add(new TextBlock{Text=S("modelLabel"),Margin=new Thickness(0,18,0,8)});var modelBox=new TextBox{Text=model,Padding=new Thickness(10)};stack.Children.Add(modelBox);
  var feedback=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};stack.Children.Add(feedback);var save=new Button{Content=S("save"),Padding=new Thickness(20,10,20,10),Margin=new Thickness(0,15,0,0)};stack.Children.Add(save);
  save.Click+=(_,_)=>{try{if(password.Password.Length>0)KeyVault.Save(password.Password);if(string.IsNullOrWhiteSpace(modelBox.Text)||modelBox.Text.Length>180)throw new InvalidDataException("Invalid model ID");model=modelBox.Text.Trim();Save();password.Clear();window.Close();status.Text=S("keySaved");}catch(Exception e){feedback.Text=e.Message;}};window.Content=stack;window.ShowDialog();
 }
}
