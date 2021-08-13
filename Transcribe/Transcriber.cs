namespace Transcribe
{
    class Transcriber
    {
        private string value;

        public Transcriber()
        {
            //
        }

        public string Get() { return this.value; }

        public void Append(string data)
        {
            this.value += data + "\n";
            if (this.value.Length > 5000)
            {
                this.value = this.value.Substring(this.value.Length - 5000);
            }
        }

        public void Prepend(string data)
        {
            this.value = data + "\n" + this.value;
            if (this.value.Length > 5000)
            {
                this.value = this.value.Substring(0, 5000);
            }
        }

        public void Set(string data)
        {
            this.value = data;
        }

        public void Clear()
        {
            this.value = "";
        }
    }
}
