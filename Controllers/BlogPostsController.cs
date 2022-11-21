﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Blog.Data;
using Blog.Models;
using Blog.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Blog.Extensions;
using Microsoft.AspNetCore.Identity;
using X.PagedList;

namespace Blog.Controllers
{
    //[Authorize(Roles = "Administrator")]
    public class BlogPostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IImageService _imageService;
        private readonly IBlogPostService _blogPostService;
        private readonly UserManager<BlogUser> _userManager;

        public BlogPostsController(ApplicationDbContext context,
                                  IImageService imageService,
                                  IBlogPostService blogPostService,
                                  UserManager<BlogUser> userManager)
        {
            _context = context;
            _imageService = imageService;
            _blogPostService = blogPostService;
            _userManager = userManager;
        }

        // GET: BlogPosts
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? pageNum)
        {
            int pageSize = 3;
            int page = pageNum ?? 1;

            // _context.BlogPosts.Include(b => b.Category);
            IPagedList<BlogPost> blogPostPage = (await _blogPostService.GetAllBlogPostsAsync())
                                            .Where(b => b.IsDeleted == false && b.IsPublished == true)
                                            .ToPagedList(page, pageSize);

            return View(blogPostPage);
        }


        // GET: BlogPosts/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(string? slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var blogPost = await _context.BlogPosts
                                         .Include(b => b.Category)
                                         .Include(b => b.Tags)
                                         .Include(b => b.Comments)
                                             .ThenInclude(c=>c.Author)
                                         .FirstOrDefaultAsync(m => m.Slug == slug);


            if (blogPost == null)
            {
                return NotFound();
            }

            return View(blogPost);
        }

        // GET: BlogPosts/Create
        [Authorize(Roles = "Administrator, Moderator")]
        public async Task<IActionResult> Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            ViewData["BlogPostTags"] = new MultiSelectList(await _blogPostService.GetTagsAsync(), "Id", "Name");
            return View(new BlogPost());
        }

        // POST: BlogPosts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator, Moderator")]
        public async Task<IActionResult> Create([Bind("Id,Title,Content,CategoryId,Abstract,IsDeleted,IsPublished,BlogPostImage")] BlogPost blogPost, string? stringTags) // Jacob Replaced / Got Rid of and Added (" IEnumerable<int> selectedTags ") with (" string stringTags ")?!
		{

            ModelState.Remove("CreatorId");

            if (ModelState.IsValid)
            {
                blogPost.CreatorId = _userManager.GetUserId(User);

                // Get Slug
                if (!await _blogPostService.ValidateSlugAsync(blogPost.Title!, blogPost.Id))
                {
                    ModelState.AddModelError("Title","A similar Title or Slug has already been used!");
                    ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", blogPost.CategoryId);
                    return View(blogPost);
                }
                blogPost.Slug = blogPost.Title!.Slugify();


                blogPost.DateCreated = DateTime.UtcNow;

                if(blogPost.BlogPostImage != null)
                {
                    blogPost.ImageData = await _imageService.ConvertFileToByteArrayAsync(blogPost.BlogPostImage);
                    blogPost.ImageType = blogPost.BlogPostImage.ContentType;
                }

                _context.Add(blogPost);
                await _context.SaveChangesAsync();

                // Jacob Replace / Got Rid of and Added (" selectedTags ") with (" stringTags ")?!
                if (!string.IsNullOrEmpty(stringTags))
                {
                    await _blogPostService.AddTagsToBlogPostAsync(stringTags, blogPost.Id);

                }                //foreach (int tagId in selectedTags)
                //{
                //    await _blogPostService.AddTagsToBlogPostAsync(categoryId, contact.Id);
                //}

                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", blogPost.CategoryId);
            ViewData["BlogPostTags"] = new MultiSelectList(await _blogPostService.GetTagsAsync(), "Id", "Name");
            return View(blogPost);
        }

        // GET: BlogPosts/Edit/5
        [Authorize(Roles = "Administrator, Moderator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var blogPost = await _context.BlogPosts.Include(b=>b.Tags).FirstOrDefaultAsync(b=>b.Id == id);

            if (blogPost == null)
            {
                return NotFound();
            }


            IEnumerable<int> blogPostTags = blogPost.Tags.Select(t=>t.Id);
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", blogPost.CategoryId);
            ViewData["BlogPostTags"] = new MultiSelectList(await _blogPostService.GetTagsAsync(), "Id", "Name", blogPostTags);

            return View(blogPost);
        }

        // POST: BlogPosts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator, Moderator")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Content,CreatorId,DateCreated,LastUpdated,CategoryId,Slug,Abstract,IsDeleted,IsPublished,ImageData,ImageType,BlogPostImage")] BlogPost blogPost, string? stringTags)
        {
            if (id != blogPost.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {

                    blogPost.DateCreated = DateTime.SpecifyKind(blogPost.DateCreated, DateTimeKind.Utc);

                    blogPost.LastUpdated = DateTime.UtcNow;

                    if (blogPost.BlogPostImage != null)
                    {
                        blogPost.ImageData = await _imageService.ConvertFileToByteArrayAsync(blogPost.BlogPostImage);
                        blogPost.ImageType = blogPost.BlogPostImage.ContentType;
                    }

                    if (!await _blogPostService.ValidateSlugAsync(blogPost.Title!, blogPost.Id))
                    {
                        ModelState.AddModelError("Title", "A similar Title or Slug has already been used!");
                        ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", blogPost.CategoryId);
                        return View(blogPost);
                    }
                    blogPost.Slug = blogPost.Title!.Slugify();



                    _context.Update(blogPost);
                    await _context.SaveChangesAsync();

					// Remove current tags
					await _blogPostService.RemoveAllBlogPostTagsAsync(blogPost.Id);

					if (!string.IsNullOrEmpty(stringTags))
					{
						await _blogPostService.AddTagsToBlogPostAsync(stringTags, blogPost.Id);

					}

					


                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogPostExists(blogPost.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", blogPost.CategoryId);
            return View(blogPost);
        }

        // GET: BlogPosts/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.BlogPosts == null)
            {
                return NotFound();
            }

            var blogPost = await _context.BlogPosts
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (blogPost == null)
            {
                return NotFound();
            }

            return View(blogPost);
        }

        // POST: BlogPosts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.BlogPosts == null)
            {
                return Problem("Entity set 'ApplicationDbContext.BlogPosts'  is null.");
            }
            var blogPost = await _context.BlogPosts.FindAsync(id);
            if (blogPost != null)
            {
                blogPost.IsDeleted = true;
                // Don't Need (" _context.BlogPosts.Remove(blogPost); ") After Added (" blogPost.IsDeleted = true; ")?!
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BlogPostExists(int id)
        {
          return _context.BlogPosts.Any(e => e.Id == id);
        }

        //public async Task<IActionResult> CategoriesView(int? pageNum)
        //{
        //    // Was (" int pageSize = 5; ")
        //    int pageSize = 3;
        //    int page = pageNum ?? 1;

        //    // Made this together with Jacob?!
        //    /* List<BlogPost> model = await _context.BlogPosts
        //                                          .Include(b => b.Comments)
        //                                          .Include(b => b.Category)
        //                                          .OrderByDescending(b => b.DateCreated)
        //                                          .ToListAsync(); */

        //    // Made this together with Antonio?! 11/8/2022
        //    IPagedList<BlogPost> model = (await _blogPostService.GetAllBlogPostsAsync())
        //                                    .Where(b => b.IsDeleted == false && b.IsPublished == true)
        //                                    .ToPagedList(page, pageSize);
        //    return View(model);
        //}
    }
}
