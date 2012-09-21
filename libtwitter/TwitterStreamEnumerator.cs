using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OAuthLib;
using System.Net;
using System.IO;

namespace Brainchild.Net
{
    public class TwitterStreamEnumerator : IEnumerator<string>
    {
        private StreamReader reader = null;
        private string buffer = null;

        internal TwitterStreamEnumerator(Stream stream)
        {
            this.reader = new StreamReader(stream);
        }

        public string Current
        {
            get { return buffer; }
        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Close();
            }
        }


        public bool MoveNext()
        {
            this.buffer = reader.ReadLine();
            if (this.buffer != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        object System.Collections.IEnumerator.Current
        {
            get { return buffer; }
        }
    }
}
