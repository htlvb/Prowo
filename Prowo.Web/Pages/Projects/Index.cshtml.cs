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
    public class IndexModel : PageModel
    {
        private readonly Prowo.Web.ProwoContext _context;

        public IndexModel(Prowo.Web.ProwoContext context)
        {
            _context = context;
        }

        public IList<Project> Project { get;set; } = default!;

        public async Task OnGetAsync()
        {
            if (_context.Projects != null)
            {
                Project = await _context.Projects.ToListAsync();
            }
        }
    }
}
