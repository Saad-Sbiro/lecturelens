using System.Net;
using System.Text;
using LectureLens.Core;
namespace LectureLens.Desktop.Adapters;

// Fixed layouts keep model output inert and printable, including right-to-left labels.
internal static class LessonDiagramHtml
{
 internal const string Styles="""
 .lesson-section{break-inside:avoid}
 .explanation{white-space:pre-wrap;margin:0 0 4mm}
 .lesson-diagram{break-inside:avoid;margin:6mm 0;padding:5mm;border:1px solid #d9d5c9;border-radius:3mm;background:#faf9f5;print-color-adjust:exact}
 .lesson-diagram figcaption{font-weight:700;margin-bottom:4mm;color:#315b54;line-height:1.35}
 .diagram-nodes{display:flex;flex-direction:column;align-items:stretch;gap:2mm}
 .diagram-node{border:1px solid #b8cfc9;border-inline-start:3px solid #56877c;background:white;border-radius:2mm;padding:2.5mm 4mm;line-height:1.4;font-size:10pt;white-space:pre-wrap}
 .diagram-arrow{text-align:center;line-height:1;color:#56877c;font-size:15pt}
 .concepts .diagram-nodes,.comparison .diagram-nodes{display:grid;grid-template-columns:1fr 1fr;gap:3mm}
 .concepts .diagram-nodes{border-inline-start:2px solid #b8cfc9;padding-inline-start:4mm}
 .concepts .diagram-node:last-child:nth-child(odd),.comparison .diagram-node:last-child:nth-child(odd){grid-column:1 / -1}
 """;

 internal static string Render(NoteDiagram diagram)
 {
  static string H(string text)=>WebUtility.HtmlEncode(text);
  var html=new StringBuilder("<figure class='lesson-diagram "+H(diagram.Kind)+"'><figcaption>"+H(diagram.Title)+"</figcaption><div class='diagram-nodes'>");
  for(int i=0;i<diagram.Labels.Length;i++) {
   if(i>0&&diagram.Kind=="sequence")html.Append("<div class='diagram-arrow' aria-hidden='true'>&#8595;</div>");
   html.Append("<div class='diagram-node'>"+H(diagram.Labels[i])+"</div>");
  }
  return html.Append("</div></figure>").ToString();
 }
}
