using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Hangfire.Highlighter.Models;

namespace Hangfire.Highlighter.Controllers
{
    public class HomeController : Controller
    {
        private readonly HighlighterDbContext  _db = new HighlighterDbContext();

        public ActionResult Index()
        {
            var codeSnippets = _db.CodeSnippets.ToList();
            return View(codeSnippets);
        }

        public ActionResult Details(int id)
        {
            var snippet = _db.CodeSnippets.Find(id);
            return View(snippet);
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Create([Bind(Include="SourceCode")] CodeSnippet snippet)
        {
            if (ModelState.IsValid)
            {
                snippet.CreatedAt = DateTime.UtcNow;

                _db.CodeSnippets.Add(snippet);
                _db.SaveChanges();

                using (StackExchange.Profiling.MiniProfiler.StepStatic("Job enqueue"))
                {
                    // Enqueue a job
                    BackgroundJob.Enqueue(() => HighlightSnippet(snippet.Id));
                }

                return RedirectToAction("Details", new { id = snippet.Id });
            }

            return View(snippet);
        }

        /*
        Process a job (code snippet highlighting) in the background
        Please, note that user still waits until its source code will be highlighted. But the application
        itself became more responsive and he is able to do another things while background job is being processed.
         */
        public static void HighlightSnippet(int snippetId)
        {
            using (var db = new HighlighterDbContext())
            {
                var snippet = db.CodeSnippets.Find(snippetId);

                if (snippet == null) return;

                snippet.HighlightedCode = HighlightSource(snippet.SourceCode);
                snippet.HighlightedAt = DateTime.UtcNow;

                db.SaveChanges();
            }
        }


        private static string HighlightSource(string source)
        {
            return RunSync(() => HighlightSourceAsync(source));
        }

        private static async Task<string> HighlightSourceAsync(string source)
        {
            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(
                    @"http://hilite.me/api",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "lexer", "c#" },
                        { "style", "vs" },
                        { "code", source },
                    }));

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
        }

        private static TResult RunSync<TResult>(Func<Task<TResult>> func)
        {
            return Task.Run<Task<TResult>>(func).Unwrap().GetAwaiter().GetResult();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }

        /*
           We are using synchronous controller action method, although it is 
         * recommended to use asynchronous one to make network 
         * calls inside ASP.NET request handling logic. As written
         * in the given article, asynchronous actions greatly increase
         * application capacity, but does not help to increase performance. 
         * You can test it by yourself with a sample application – there are
         * no differences in using sync or async actions with a single request.
         *
         *  This sample is aimed to show you the problems related to application performance.
         *  And sync actions are used only to keep the tutorial simple.
         *  
         * */

    }
}