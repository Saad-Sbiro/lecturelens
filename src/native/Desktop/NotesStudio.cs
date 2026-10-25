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
 record Preferences(string Detail,string Language,bool IncludeSlides,string Model,bool KeepRaw, string? TeacherLanguage = null, bool CloudSpeech = false, int ModelPreferenceVersion = 0);
 readonly DataGrid sessions=(DataGrid)view.FindName("Sessions");
 readonly TextBlock status=(TextBlock)view.FindName("SessionMessage");
 readonly ComboBox detail=(ComboBox)view.FindName("Detail");
 readonly ComboBox language=(ComboBox)view.FindName("PdfLanguage");
 readonly ComboBox teacher=(ComboBox)view.FindName("TeacherLanguage");
 string model=NvidiaNotes.DefaultModel;
 CancellationTokenSource? running;
 internal static bool IsGenerating { get; private set; }
 string S(string key)=>(string)view.FindResource(key);
 string PrefPath=>Path.Combine(KeyVault.Folder,"notes-preferences.json");
 string? Folder=>sessions.SelectedItem is SessionInfo s&&s.State!="recording"&&s.Problems.Length==0?Path.Combine(LectureLens.Storage.LocalStore.DefaultRoot,s.Id):null;
 internal void Attach()
 {
  Motion.Initialize();
  WorkspaceMotion.Attach(view);
  Notifications.Attach(view);
  detail.ItemsSource=new[]{new Choice("brief",S("brief")),new Choice("standard",S("standard")),new Choice("detailed",S("detailed"))};detail.DisplayMemberPath="Label";detail.SelectedValuePath="Id";detail.SelectedValue="standard";
  using var registry=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"languages.json")));
  var choices=registry.RootElement.EnumerateObject().Where(x=>x.Value.GetProperty("enabled").GetBoolean()).Select(x=>new Choice(x.Name,x.Value.GetProperty("nativeName").GetString()!)).ToArray();
  language.ItemsSource=choices;language.DisplayMemberPath="Label";language.SelectedValuePath="Id";language.SelectedValue=registry.RootElement.EnumerateObject().First(x=>x.Value.GetProperty("defaultOutput").GetBoolean()).Name;
  var teacherChoices=choices.Where(c=>!registry.RootElement.GetProperty(c.Id).TryGetProperty("speechEnabled",out var enabled)||enabled.GetBoolean()).ToArray();
  teacher.ItemsSource=teacherChoices;teacher.DisplayMemberPath="Label";teacher.SelectedValuePath="Id";teacher.SelectedValue=registry.RootElement.EnumerateObject().First(x=>x.Value.GetProperty("defaultTeacher").GetBoolean()).Name;
  if(File.Exists(PrefPath))try{var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PrefPath))!;if(new[]{"brief","standard","detailed"}.Contains(p.Detail))detail.SelectedValue=p.Detail;if(choices.Any(c=>c.Id==p.Language))language.SelectedValue=p.Language;model=p.ModelPreferenceVersion==0&&p.Model=="nvidia/nemotron-3.5-lightning-30b-a3b"?NvidiaNotes.DefaultModel:p.Model;if(teacherChoices.Any(c=>c.Id==p.TeacherLanguage))teacher.SelectedValue=p.TeacherLanguage;}catch(JsonException){}
  Button("SavePreferences").Click+=(_,_)=>{Save();status.Text=S("preferencesSaved");};
  Button("KeySetup").Click+=(_,_)=>Settings();
  Button("Transcribe").Click+=async(_,_)=>await TranscribeLesson();
  Button("OpenPdf").Click+=(_,_)=>OpenPdf();
  Button("ImportTranscript").Click+=(_,_)=>Import();
  Button("GeneratePdf").Click+=async(_,_)=>await Generate();
  Button("CancelGeneration").Click+=(_,_)=>running?.Cancel();
  Button("CancelGeneration").IsEnabled=false;
  string? animatedLesson=null;
  sessions.SelectionChanged+=(_,_)=>{
   UpdateSource();
   if(sessions.SelectedItem is SessionInfo selected&&selected.Id!=animatedLesson){animatedLesson=selected.Id;Motion.Enter((TextBox)view.FindName("TranscriptPreview"));}
  };UpdateSource();
  view.Unloaded+=(_,_)=>((TextBox)view.FindName("TranscriptPreview")).BeginAnimation(UIElement.OpacityProperty,null);
 }
 record Choice(string Id,string Label);
 Button Button(string name)=>(Button)view.FindName(name);
 NotesPreferences Options()=>new((string)detail.SelectedValue,(string)language.SelectedValue,true,sessions.SelectedItem is SessionInfo lesson?lesson.Title:null);
 void Save(){Directory.CreateDirectory(KeyVault.Folder);var o=Options();File.WriteAllText(PrefPath+".partial",JsonSerializer.Serialize(new Preferences(o.Detail,o.Language,true,model,true,(string)teacher.SelectedValue,true,1)));File.Move(PrefPath+".partial",PrefPath,true);}
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
   Button("Transcribe").IsEnabled=running==null&&folder!=null&&sessions.SelectedItem is SessionInfo item&&item.State!="archived";
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
 async Task EnsureTranscript(string folder,CancellationToken token,bool replace=false)
 {
  if(!replace&&LessonTranscript.Load(folder).Length>0)return;
  if(!KeyVault.HasSpeechKey)throw new InvalidOperationException(S("speechKeyRequired"));
  if(!File.Exists(Path.Combine(folder,"audio.wav"))&&!File.Exists(Path.Combine(folder,"audio.webm"))&&!File.Exists(Path.Combine(folder,"recording.webm")))await Task.Run(()=>new LectureLens.Storage.LocalStore(LectureLens.Storage.LocalStore.DefaultRoot).Recover(Path.GetFileName(folder)),token);
  var transcript=await new LocalSpeech(new GroqSpeech(true)).Transcribe(folder,(string)teacher.SelectedValue,new Progress<string>(message=>status.Text=message),token);
  LessonTranscript.Save(folder,LessonTranscript.Import(transcript));
  UpdateSource();
 }
 async Task TranscribeLesson()
 {
  if(running!=null||Folder is not string folder)return;
  if(LessonTranscript.Load(folder).Length>0&&MessageBox.Show(S("replaceTranscript"),"LectureLens",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  if(!KeyVault.HasSpeechKey){Connections(true);return;}
  BeginJob();
  try {Save();await EnsureTranscript(folder,running!.Token,true);status.Text=S("transcriptReady");}
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
  Button("ImportTranscript").IsEnabled=true;Button("CancelGeneration").IsEnabled=false;sessions.IsEnabled=true;
  UpdateSource();
 } internal static List<NoteSource[]> Parts(IReadOnlyList<NoteSource> sources)
 =>SlideTimeline.Parts(sources);

 async Task Generate()
 {
  if(running!=null)return;
  var folder=Folder;if(folder==null){status.Text=S("chooseSession");return;}
  if(!KeyVault.HasKey || (!KeyVault.HasSpeechKey && LessonTranscript.Load(folder).Length==0)){Connections();return;}
  
  BeginJob();


  try
  {
   Save();
   var options=Options();
   await EnsureTranscript(folder,running!.Token);
   var loaded=LessonTranscript.Load(folder);
   // Upgrade only equivalent old speech imports, preserving edited/imported text.
   var speechFile=Path.Combine(folder,"speech-transcript.json");
   if(loaded.All(s=>s.TimestampBasis==null)&&File.Exists(speechFile)) {
    var timed=LessonTranscript.Import(speechFile);
    if(loaded.Select(s=>(s.Id,s.Text)).SequenceEqual(timed.Select(s=>(s.Id,s.Text))))loaded=timed;
   }
   var sources=await SlideTimeline.Align(folder,loaded,running.Token);
   var parts=Parts(sources);
   ITextGenerator generator=new NvidiaNotes(model,Path.Combine(folder,"notes-cache"));
   var outputs=new List<StudyNotes>();
   for(int i=0;i<parts.Count;i++)
   {
    status.Text=S("progressPart")+" "+(i+1)+" / "+parts.Count;
    outputs.Add(await generator.Generate(parts[i],options,running.Token));
   }
   var notes=new StudyNotes(options.Topic??outputs[0].Title,outputs.Count==1?outputs[0].Overview:"",outputs.SelectMany(n=>n.Sections).ToArray(),outputs.SelectMany(n=>n.Decisions??[]).ToArray());
   var destination=Path.GetFullPath(Path.Combine(LectureLens.Storage.LocalStore.DefaultRoot,"..","exports",Path.GetFileName(folder),DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]));
   Directory.CreateDirectory(destination);
   await File.WriteAllTextAsync(Path.Combine(destination,"sources.json"),JsonSerializer.Serialize(sources,NvidiaNotes.Json));
   await File.WriteAllTextAsync(Path.Combine(destination,"notes.json"),JsonSerializer.Serialize(notes,NvidiaNotes.Json));
   await File.WriteAllTextAsync(Path.Combine(destination,"preferences.json"),JsonSerializer.Serialize(options));
   var pdf=await new BrowserPdf().Render(notes,sources,options,folder,destination,running.Token);
   File.WriteAllText(Path.Combine(folder,"latest-export.json.partial"),JsonSerializer.Serialize(new{path=pdf,createdAt=DateTimeOffset.UtcNow}));
   File.Move(Path.Combine(folder,"latest-export.json.partial"),Path.Combine(folder,"latest-export.json"),true);
   status.Text="";
   EndJob();
   Notifications.PdfReady(view,pdf);
  }
  catch(OperationCanceledException){status.Text=S("cancel");}
  catch(Exception e){status.Text=e.Message;}
  finally{if(running!=null)EndJob();}
 }
 void SetEditing(bool enabled)
 {
  foreach(var name in new[]{"MergeRecordings","Refresh","Recover","ViewSlides","Stop","Delete","SavePreferences","KeySetup","RenameLesson","OpenPdf"})
   Button(name).IsEnabled=enabled;
  teacher.IsEnabled=enabled;((TextBox)view.FindName("SearchLessons")).IsEnabled=enabled;
  detail.IsEnabled=enabled;language.IsEnabled=enabled;
 }
 void Settings()
 {
  var stack=new StackPanel();
  stack.Children.Add(new TextBlock{Text=S("keyTitle"),FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,20)});
  var appearance=AppearanceSettings.Load();
  stack.Children.Add(new TextBlock{Text=S("themeLabel"),Margin=new Thickness(0,0,0,8)});
  var themeBox=new ComboBox{ItemsSource=new[]{new Choice("dark",S("darkTheme")),new Choice("light",S("lightTheme"))},DisplayMemberPath="Label",SelectedValuePath="Id",SelectedValue=appearance.Theme};stack.Children.Add(themeBox);
  stack.Children.Add(new TextBlock{Text=S("fontLabel"),Margin=new Thickness(0,16,0,8)});
  var fontBox=new ComboBox{ItemsSource=AppearanceSettings.Fonts,SelectedItem=appearance.Font};stack.Children.Add(fontBox);
  var feedback=new TextBlock{Margin=new Thickness(0,12,0,0)};stack.Children.Add(feedback);
  var actions=new WrapPanel{Margin=new Thickness(0,20,0,0)};stack.Children.Add(actions);
  var save=new Button{Content=S("save"),Style=(Style)view.FindResource("Primary")};actions.Children.Add(save);
  var cancel=new Button{Content=S("cancelGeneration")};actions.Children.Add(cancel);
  var terms=new Button{Content=LectureLens.Storage.TermsAgreement.Document.Title};actions.Children.Add(terms);
  var dialog=new InAppDialog(view,stack,520);dialog.Dismiss=dialog.Close;
  terms.Click+=(_,_)=>{dialog.Close();TermsNotice.Show(view,false);};
  cancel.Click+=(_,_)=>dialog.Close();
  save.Click+=(_,_)=>{
   try {
    var choice=new AppearanceSettings.Choice((string)themeBox.SelectedValue,(string)fontBox.SelectedItem);
    AppearanceSettings.Save(choice);AppearanceSettings.Apply(Application.Current,choice);
    dialog.Close();status.Text=S("preferencesSaved");
   }catch(Exception e){feedback.Text=e.Message;}
  };
 }
 void Connections(bool speechOnly=false)
 {
  var window=new Window{Title=S("connectionsLabel"),Width=600,Height=530,Owner=Window.GetWindow(view),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
  window.SetResourceReference(Window.BackgroundProperty,"PanelBackground");window.SetResourceReference(Window.ForegroundProperty,"AppForeground");window.SetResourceReference(Window.FontFamilyProperty,"AppFont");
  var stack=new StackPanel{Margin=new Thickness(24)};
  stack.Children.Add(new TextBlock{Text=S("connectionsLabel"),FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,12)});
  stack.Children.Add(new TextBlock{Text=S("keyLabel"),Margin=new Thickness(0,0,0,8)});
  var password=new PasswordBox{Padding=new Thickness(10)};stack.Children.Add(password);
  stack.Children.Add(new TextBlock{Text=S("modelLabel"),Margin=new Thickness(0,18,0,8)});
  var modelBox=new ComboBox{IsEditable=true,Text=model,Padding=new Thickness(10),ItemsSource=new[]{NvidiaNotes.DefaultModel,"nvidia/nemotron-3.5-lightning-30b-a3b"}};stack.Children.Add(modelBox);
  stack.Children.Add(new TextBlock{Text=S("speechKeyLabel"),Margin=new Thickness(0,18,0,8)});
  var speechPassword=new PasswordBox{Padding=new Thickness(10)};stack.Children.Add(speechPassword);
  if(speechOnly)foreach(var item in stack.Children.Cast<UIElement>().Skip(1).Take(4))item.Visibility=Visibility.Collapsed;
  stack.Children.Add(new TextBlock{Text=S("speechFreeHint"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
  var feedback=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};stack.Children.Add(feedback);
  var help=new Button{Content=S("getGroqKey"),Margin=new Thickness(0,12,0,0)};stack.Children.Add(help);
  help.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo("https://console.groq.com/keys"){UseShellExecute=true});}catch(Exception e){feedback.Text=e.Message;}};
  var save=new Button{Content=S("save"),Padding=new Thickness(20,10,20,10),Margin=new Thickness(0,15,0,0)};stack.Children.Add(save);
  save.Click+=(_,_)=>{
   try {
    if(string.IsNullOrWhiteSpace(modelBox.Text)||modelBox.Text.Length>180)throw new InvalidDataException("Invalid model ID");
    if(password.Password.Length>0)KeyVault.Save(password.Password);
    if(speechPassword.Password.Length>0)KeyVault.Save(speechPassword.Password,true);
    model=modelBox.Text.Trim();Save();password.Clear();speechPassword.Clear();window.Close();status.Text=S("keySaved");
   }catch(Exception e){feedback.Text=e.Message;}
  };
  window.Content=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};window.ShowDialog();
 }
}

