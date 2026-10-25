using System.IO;
using LectureLens.Core;
using LectureLens.Desktop.Adapters;

internal static class LessonContentChecks
{
 internal static readonly NoteSource[] DemoSources=[
  new("S1","lesson","Le cycle commence par analyser les besoins, puis concevoir la solution, développer et tester. Les tests comparent le comportement observé au résultat attendu."),
  new("S2","audience","Vous pouvez répéter ? Est-ce que le devoir est pour demain ?"),
  new("S3","mixed","Mon micro fonctionne ? L'analyse décrit le besoin ; la conception décrit la solution. Ces deux activités font partie du cycle logiciel.")];
 internal static readonly StudyNotes DemoNotes=new("Du besoin à la validation","Le cycle logiciel relie l'analyse des besoins à la vérification de la solution.",[
  new("Comprendre le cycle logiciel",[],["S1"],["Le cycle commence par l'analyse des besoins. La conception organise ensuite la solution, avant son développement. Les tests comparent enfin le comportement observé au résultat attendu."],
   [new("sequence","Du besoin aux tests",["Analyser les besoins","Concevoir la solution","Développer","Tester le comportement"],["S1"])]),
  new("Distinguer le besoin et la solution",[],["S3"],["L'analyse décrit le besoin. La conception décrit la solution. Ces deux activités appartiennent au cycle logiciel, mais répondent à des objectifs différents."],
   [new("comparison","Deux objectifs distincts",["Analyse : décrire le besoin","Conception : décrire la solution"],["S3"]),new("concepts","Activités du cycle logiciel",["Analyse des besoins","Conception de la solution"],["S3"])])
 ],[new("S1","keep","Explains the lesson process"),new("S2","exclude","Audience request and homework logistics"),new("S3","mixed","Keep the conceptual distinction; omit microphone troubleshooting")]);
 internal static readonly StudyNotes ArabicNotes=new("من الحاجة إلى التحقق","تشرح هذه الملاحظات مراحل بناء الحل والتحقق منه.",[
  new("مراحل تطوير الحل",[],["S1"],["تبدأ العملية بتحليل الاحتياجات، ثم تصميم الحل وتطويره. تقارن الاختبارات (tests) السلوك الفعلي بالنتيجة المتوقعة."],
   [new("sequence","من التحليل إلى الاختبار",["تحليل الاحتياجات","تصميم الحل","تطوير الحل","اختبار السلوك"],["S1"])]),
  new("الحاجة والحل",[],["S3"],["يصف التحليل الحاجة، بينما يصف التصميم الحل. كلاهما جزء من دورة تطوير البرمجيات."],
   [new("comparison","مقارنة الهدف",["التحليل: وصف الحاجة","التصميم: وصف الحل"],["S3"])])
 ],DemoNotes.Decisions);

 internal static async Task Run(string root)
 {
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
  void Reject(StudyNotes notes,string name){try{NotesValidation.ValidateLesson(notes,DemoSources);}catch(InvalidDataException){Console.WriteLine("PASS "+name);return;}throw new Exception(name);}
  NotesValidation.ValidateLesson(DemoNotes,DemoSources);
  Reject(DemoNotes with{Decisions=null},"missing selection audit rejected");
  Reject(DemoNotes with{Sections=[DemoNotes.Sections[0]]},"retained final concept cannot disappear");
  Reject(DemoNotes with{Sections=[DemoNotes.Sections[0] with{Paragraphs=null},DemoNotes.Sections[1]]},"bullet-only lesson rejected");
  Reject(DemoNotes with{Sections=[DemoNotes.Sections[0] with{SourceIds=["S1","S2"]},DemoNotes.Sections[1]]},"audience-only source cannot support lesson prose");
  Reject(DemoNotes with{Sections=[DemoNotes.Sections[0] with{Diagrams=[new("sequence","Wrong",["a","b"],["S3"])]},DemoNotes.Sections[1]]},"diagram cannot cite unrelated section");
  var empty=new StudyNotes("Empty lesson","",[],DemoSources.Select(s=>new SourceDecision(s.Id,"exclude","No educational content")).ToArray());
  NotesValidation.ValidateLesson(empty,DemoSources);
  var html=BrowserPdf.Compose(DemoNotes,DemoSources,"fr","standard",[]);
  Check(html.Contains("<p class='explanation'>")&&!html.Contains("<ul>")&&html.Contains("L&#39;analyse"),"paragraphs rendered with no empty bullet lists");
  Check(!html.Contains("devoir")&&!html.Contains("micro fonctionne"),"renderer uses selected notes, never raw conversation");
  Check(html.Contains("lesson-diagram sequence")&&html.Contains("lesson-diagram concepts")&&html.Contains("lesson-diagram comparison"),"all supported visual layouts render");
  Check(html.Split("&#8595;").Length==4,"only ordered stages have arrows");
  var hostile=DemoNotes with{Sections=[DemoNotes.Sections[0] with{Diagrams=[new("concepts","<script>",["<img src=x>","A & B"],["S1"])]}]};
  Check(!BrowserPdf.Compose(hostile,DemoSources,"fr","standard",[]).Contains("<script>"),"diagram text cannot inject HTML");
  var output=Path.Combine(root,"lesson.html");
  await BrowserPdf.WriteHtml(DemoNotes,DemoSources,new("standard","fr",false),root,output,CancellationToken.None);
  Check((await File.ReadAllTextAsync(output)).Contains("lesson-diagram sequence"),"production streaming export retains diagrams without screenshots");
  Check(BrowserPdf.Compose(ArabicNotes,DemoSources,"ar","standard",[]).Contains("dir='rtl'"),"Arabic registry keeps reading direction");
 }
}
