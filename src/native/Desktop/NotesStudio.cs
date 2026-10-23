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
 record Preferences(string Detail,string Language,bool IncludeSlides,string Model,bool KeepRaw, string? TeacherLanguage = null);
 readonly DataGrid sessions=(DataGrid)view.FindName("Sessions");
 readonly TextBlock status=(TextBlock)view.FindName("SessionMessage");
 readonly ComboBox detail=(ComboBox)view.FindName("Detail");
 readonly ComboBox language=(ComboBox)view.FindName("PdfLanguage");
 readonly CheckBox images=(CheckBox)view.FindName("IncludeSlides");
 readonly ComboBox teacher=(ComboBox)view.FindName("TeacherLanguage");
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
  teacher.ItemsSource=choices;teacher.DisplayMemberPath="Label";teacher.SelectedValuePath="Id";teacher.SelectedValue=registry.RootElement.EnumerateObject().First(x=>x.Value.GetProperty("defaultTeacher").GetBoolean()).Name;
  if(File.Exists(PrefPath))try{var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PrefPath))!;if(new[]{"brief","standard","detailed"}.Contains(p.Detail))detail.SelectedValue=p.Detail;if(choices.Any(c=>c.Id==p.Language))language.SelectedValue=p.Language;images.IsChecked=p.IncludeSlides;model=p.Model;keep.IsChecked=p.KeepRaw;if(choices.Any(c=>c.Id==p.TeacherLanguage))teacher.SelectedValue=p.TeacherLanguage;}catch(JsonException){}
  Button("SavePreferences").Click+=(_,_)=>{Save();status.Text=S("preferencesSaved");};
  Button("KeySetup").Click+=(_,_)=>Settings();
  Button("Transcribe").Click+=async(_,_)=>await TranscribeLesson();
  Button("OpenPdf").Click+=(_,_)=>OpenPdf();
  Button("ImportTranscript").Click+=(_,_)=>Import();
  Button("GeneratePdf").Click+=async(_,_)=>await Generate();
  Button("CancelGeneration").Click+=(_,_)=>running?.Cancel();
  Button("CancelGeneration").IsEnabled=false;
  sessions.SelectionChanged+=(_,_)=>{((CheckBox)view.FindName("CloudConsent")).IsChecked=false;UpdateSource();};UpdateSource();
 }
 record Choice(string Id,string Label);
 Button Button(string name)=>(Button)view.FindName(name);
 NotesPreferences Options()=>new((string)detail.SelectedValue,(string)language.SelectedValue,images.IsChecked==true);
 void Save(){Directory.CreateDirectory(KeyVault.Folder);var o=Options();File.WriteAllText(PrefPath+".partial",JsonSerializer.Serialize(new Preferences(o.Detail,o.Language,o.IncludeSlides,model,keep.IsChecked==true,(string)teacher.SelectedValue)));File.Move(PrefPath+".partial",PrefPath,true);}
 void UpdateSource()
 {
  var text=(TextBlock)view.FindName("TranscriptStatus");
  var preview=(TextBox)view.FindName("TranscriptPreview");
  var title=(TextBlock)view.FindName("LessonTitle");
  title.Text=sessions.SelectedItem is SessionInfo selected?selected.Title:S("chooseSession");
  try {
   var folder=Folder;
   var sources=folder==null?[]:LessonTranscript.Load(folder);
   preview.Text=string.Join("\n\n",sources.Select(s=>s.Label+"\n"+s.Text));
   text.Text=sources.Length>0?S("transcriptReady"):S("transcriptMissing");
   Button("Transcribe").IsEnabled=running==null&&folder!=null&&sources.Length==0&&sessions.SelectedItem is SessionInfo item&&item.State!="archived";
   Button("GeneratePdf").IsEnabled=running==null&&folder!=null;
   Button("OpenPdf").IsEnabled=running==null&&folder!=null&&File.Exists(Path.Combine(folder,"latest-export.json"));
  } catch(Exception e){preview.Text="";text.Text=e.Message;}
 }
 void Import()
 {
  try {
   var folder=Folder;if(folder==null){status.Text=S("chooseSession");return;}
   var dialog=new OpenFileDialog{Filter=S("sourceSelect"),Title=S("attachMismatch")};if(dialog.ShowDialog()!=true)return;
   LessonTranscript.Save(folder,LessonTranscript.Import(dialog.FileName));
   UpdateSource();status.Text=S("transcriptReady");
  }catch(Exception e){status.Text=e.Message;}
 }
 void OpenPdf()
 {
  try {
   if(Folder is not string folder)return;
   using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"latest-export.json")));
   var path=doc.RootElement.GetProperty("path").GetString()!;
   var exports=Path.GetFullPath(Path.Combine(LectureLens.Storage.LocalStore.DefaultRoot,"..","exports"))+Path.DirectorySeparatorChar;
   if(!Path.GetFullPath(path).StartsWith(exports,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||Path.GetExtension(path)!=".pdf")throw new IOException("Saved PDF not found.");
   Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
  }catch(Exception e){status.Text=e.Message;}
 }
 async Task EnsureTranscript(string folder,CancellationToken token)
 {
  if(LessonTranscript.Load(folder).Length>0)return;
  if(!File.Exists(Path.Combine(folder,"audio.wav"))&&!File.Exists(Path.Combine(folder,"audio.webm"))&&!File.Exists(Path.Combine(folder,"recording.webm")))await Task.Run(()=>new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot).Recover(Path.GetFileName(folder)),token);
  var transcript=await new LocalSpeech().Transcribe(folder,(string)teacher.SelectedValue,new Progress<string>(message=>status.Text=message),token);
  LessonTranscript.Save(folder,LessonTranscript.Import(transcript));
  UpdateSource();
 }
 async Task TranscribeLesson()
 {
  if(running!=null||Folder is not string folder)return;
  BeginJob();
  try {Save();await EnsureTranscript(folder,running!.Token);status.Text=S("transcriptReady");}
  catch(OperationCanceledException){status.Text=S("cancel");}
  catch(Exception e){status.Text=e.Message;}
  finally {EndJob();}
 }
 void BeginJob()
 {
  running=new CancellationTokenSource();IsGenerating=true;SetEditing(false);
  Button("GeneratePdf").IsEnabled=false;Button("ImportTranscript").IsEnabled=false;
  Button("Transcribe").IsEnabled=false;Button("CancelGeneration").IsEnabled=true;sessions.IsEnabled=false;
 }
 void EndJob()
 {
  running?.Dispose();running=null;IsGenerating=false;SetEditing(true);
  ((CheckBox)view.FindName("CloudConsent")).IsChecked=false;
  Button("ImportTranscript").IsEnabled=true;Button("CancelGeneration").IsEnabled=false;sessions.IsEnabled=true;
  UpdateSource();
 } internal static List<NoteSource[]> Parts(IReadOnlyList<NoteSource> sources)
 {
  var result=new List<NoteSource[]>();var batch=new List<NoteSource>();int count=0;
  foreach(var s in sources){if(count+s.Text.Length>10000&&batch.Count>0){result.Add(batch.ToArray());batch.Clear();count=0;}batch.Add(s);count+=s.Text.Length;}if(batch.Count>0)result.Add(batch.ToArray());return result;
 }
 async Task Generate()
 {
  if(running!=null)return;
  var folder=Folder;if(folder==null){status.Text=S("chooseSession");return;}
  var cloud=((CheckBox)view.FindName("CloudConsent")).IsChecked==true;
  if(!cloud && !Equals(teacher.SelectedValue,language.SelectedValue)){status.Text=S("offlineLanguageMismatch");return;}
  if(cloud&&!KeyVault.HasKey){Settings();return;}
  
  BeginJob();


  try
  {
   Save();
   var options=Options();var retain=keep.IsChecked==true;
   await EnsureTranscript(folder,running!.Token);
   var sources=LessonTranscript.Load(folder);
   var parts=Parts(sources);
   ITextGenerator generator=cloud?new NvidiaNotes(model,Path.Combine(folder,"notes-cache")):new ExtractiveNotes(((SessionInfo)sessions.SelectedItem).Title);
   var outputs=new List<StudyNotes>();
   for(int i=0;i<parts.Count;i++)
   {
    status.Text=S("progressPart")+" "+(i+1)+" / "+parts.Count;
    outputs.Add(await generator.Generate(parts[i],options,running.Token));
   }
   var notes=new StudyNotes(outputs[0].Title,string.Join("\n\n",outputs.Select(n=>n.Overview).Distinct()),outputs.SelectMany(n=>n.Sections).ToArray());
   var destination=Path.GetFullPath(Path.Combine(LectureLens.Storage.LocalStore.DefaultRoot,"..","exports",Path.GetFileName(folder),DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]));
   Directory.CreateDirectory(destination);
   await File.WriteAllTextAsync(Path.Combine(destination,"notes.json"),JsonSerializer.Serialize(notes,NvidiaNotes.Json));
   await File.WriteAllTextAsync(Path.Combine(destination,"preferences.json"),JsonSerializer.Serialize(options));
   var pdf=await new BrowserPdf().Render(notes,sources,options,folder,destination,running.Token);
   File.WriteAllText(Path.Combine(folder,"latest-export.json.partial"),JsonSerializer.Serialize(new{path=pdf,createdAt=DateTimeOffset.UtcNow}));
   File.Move(Path.Combine(folder,"latest-export.json.partial"),Path.Combine(folder,"latest-export.json"),true);
   status.Text=S("done")+" "+pdf;
   Process.Start(new ProcessStartInfo(pdf){UseShellExecute=true});
   if(!retain&&MessageBox.Show(S("retentionConfirm"),"LectureLens",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)
   {
    var id=Path.GetFileName(folder);
    using var store=new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot);
    await Task.Run(()=>store.RemoveRaw(id));
    var items=await Task.Run(store.List);
    sessions.ItemsSource=items;sessions.SelectedItem=items.FirstOrDefault(s=>s.Id==id);
   }
  }
  catch(OperationCanceledException){status.Text=S("cancel");}
  catch(Exception e){status.Text=e.Message;}
  finally{EndJob();}
 }
 void SetEditing(bool enabled)
 {
  foreach(var name in new[]{"MergeRecordings","Refresh","Recover","ViewSlides","Stop","Delete","SavePreferences","KeySetup","RenameLesson","OpenPdf"})
   Button(name).IsEnabled=enabled;
  teacher.IsEnabled=enabled;((TextBox)view.FindName("SearchLessons")).IsEnabled=enabled;
  detail.IsEnabled=enabled;language.IsEnabled=enabled;images.IsEnabled=enabled;keep.IsEnabled=enabled;
  ((CheckBox)view.FindName("CloudConsent")).IsEnabled=enabled;
 }
 void Settings()
 {
  var window=new Window{Title=S("keyTitle"),Width=540,Height=340,Owner=Window.GetWindow(view),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
  var stack=new StackPanel{Margin=new Thickness(24)};
  stack.Children.Add(new TextBlock{Text=S("keyLabel"),Margin=new Thickness(0,0,0,8)});
  var password=new PasswordBox{Padding=new Thickness(10)};stack.Children.Add(password);
  stack.Children.Add(new TextBlock{Text=S("modelLabel"),Margin=new Thickness(0,18,0,8)});
  var modelBox=new TextBox{Text=model,Padding=new Thickness(10)};stack.Children.Add(modelBox);
  var feedback=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};stack.Children.Add(feedback);
  var save=new Button{Content=S("save"),Padding=new Thickness(20,10,20,10),Margin=new Thickness(0,15,0,0)};stack.Children.Add(save);
  save.Click+=(_,_)=>{
   try {
    if(string.IsNullOrWhiteSpace(modelBox.Text)||modelBox.Text.Length>180)throw new InvalidDataException("Invalid model ID");
    if(password.Password.Length>0)KeyVault.Save(password.Password);
    model=modelBox.Text.Trim();Save();password.Clear();window.Close();status.Text=S("keySaved");
   }catch(Exception e){feedback.Text=e.Message;}
  };
  window.Content=stack;window.ShowDialog();
 }
}

