using DotLiquid;
using iText.Html2pdf;
using iText.Html2pdf.Resolver.Font;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prowo.Web.Data;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Prowo.Web.Controllers
{
    [Route("projects/report")]
    [ApiController]
    public class ProjectReportController : Controller
    {
        private readonly ProjectStore projectStore;
        private readonly UserStore userStore;

        public ProjectReportController(ProjectStore projectStore, UserStore userStore)
        {
            this.projectStore = projectStore;
            this.userStore = userStore;
        }

        [Authorize(Roles = "Report.Create")]
        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetProjectAttendees(string projectId)
        {
            var project = await projectStore.Get(projectId); // TODO handle not found
            var attendees = project.CalculateActualAttendees();

            var mdTemplate = System.IO.File.ReadAllText(@".\Templates\ProjectAttendeesReport.md");
            var templateArgs = new
            {
                project = new
                {
                    title = project.Title,
                    date = DateOnly.ParseExact(project.Date, "d", CultureInfo.InvariantCulture).ToLongDateString(),
                    attendees = attendees.Select(v =>
                        new
                        {
                            firstName = v.FirstName,
                            lastName = v.LastName,
                            @class = v.Class
                        }).ToArray()
                }
            };
            var template = Template.Parse(mdTemplate);
            var mdDocument = template.Render(Hash.FromAnonymousObject(templateArgs), CultureInfo.GetCultureInfo("de-AT"));

            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlDocument = Markdown.ToHtml(mdDocument, pipeline);

            //return Content(htmlDocument, "text/html");

            ConverterProperties htmlToPdfConverterProperties = new();
            //htmlToPdfConverterProperties.SetFontProvider(new DefaultFontProvider(true, true, true));
            var pdfStream = new MemoryStream();
            HtmlConverter.ConvertToPdf(htmlDocument, pdfStream, htmlToPdfConverterProperties);
            var pdfBytes = pdfStream.ToArray();

            return File(pdfBytes, "application/pdf");
        }
    }
}
