using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentDB.Samples.OrderBy.Models
{
    class Status
    {
        public long StatusId;

        public string Text;

        public User User;

        public DateTime CreatedAt;

        public Entity[] Entities;
    }
}
