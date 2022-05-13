using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Prowo.Web;
using Prowo.Web.Models;

namespace Prowo.Web.Pages.Projects
{
    public class DetailsModel : PageModel
    {
        private readonly Prowo.Web.ProwoContext _context;

        public DetailsModel(Prowo.Web.ProwoContext context)
        {
            _context = context;
        }

      public Project Project { get; set; } = default!; 

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null || _context.Projects == null)
            {
                return NotFound();
            }

            var project = await _context.Projects.FirstOrDefaultAsync(m => m.Id == id);
            if (project == null)
            {
                return NotFound();
            }
            else 
            {
                Project = project;
            }
            return Page();
        }
    }
}
