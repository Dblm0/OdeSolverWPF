using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OdeSolverWPF
{
    class ErrorWord
    {
        private Int32 _start;
        private Int32 _length;
        private String _word;

        public Int32 StartIndex { get { return _start; } }
        public Int32 Length { get { return _length; } }

        public String Word { get { return _word; } }

        public ErrorWord(Int32 start, String word)
        {
            _start = start;
            _word = word;
            _length = _word.Length;  
        }

    }
}
