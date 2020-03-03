using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DataBase
{
    public class ImagesContext : DbContext
    {
        public DbSet<Image> Images { get; set; }


        public ImagesContext()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source = /Users/dari/Documents/onnx/ClassifiedImages.db");
    }

    public class Image
    {
        public Image()
        {
            this.AccessCount = 0;
        }
        [Key]
        public int ImgId { get; set; }
        public string Name { get; set; }
        public int Class { get; set; }
        public int AccessCount { get; set; }
        public long FileHash { get; set; }

        public int BlobID { get; set; }
        public virtual byte[] FileContent { get; set; }
    }
}

