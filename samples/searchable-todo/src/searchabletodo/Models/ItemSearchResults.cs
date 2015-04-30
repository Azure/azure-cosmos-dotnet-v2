using System;
using System.Collections.Generic;

namespace searchabletodo.Models
{
    public class ItemSearchResults
    {
        public int TotalCount { get; set; }

        public IEnumerable<Item> Items { get; set; }

        public IEnumerable<Tuple<string, int>> TagCounts { get; set; }

        public IEnumerable<Tuple<string, int>> DateCounts { get; set; }
   }
}