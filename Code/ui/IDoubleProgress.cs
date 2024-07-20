using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chinese_Name.ui
{
    internal interface IDoubleProgress<TTop, TProgress> : IProgress<TProgress>
    {
        public void Report(TTop top);
    }
}
