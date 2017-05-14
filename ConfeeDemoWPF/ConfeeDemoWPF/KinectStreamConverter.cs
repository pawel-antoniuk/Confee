using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfeeDemoWPF
{
    class KinectStreamConverter : Stream
    {
        private readonly Stream _kinect32BitStream;

        public KinectStreamConverter(Stream input)
        {
            this._kinect32BitStream = input;
        }

        public bool SpeechActive { get; set; }

        public override bool CanRead => true;

        public override bool CanWrite => false;

        public override bool CanSeek => false;

        public override long Position
        {
            get => 0;
            set => throw new NotImplementedException();
        }

        public override long Length => throw new NotImplementedException();

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return 0;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            const int sampleSizeRatio = sizeof(float) / sizeof(short);
            const int sleepDuration = 50;
            var readcount = count * sampleSizeRatio;
            var kinectBuffer = new byte[readcount];
            var bytesremaining = readcount;

            while (bytesremaining > 0)
            {
                if (!this.SpeechActive)
                {
                    return 0;
                }

                var result = this._kinect32BitStream.Read(kinectBuffer, readcount - bytesremaining, bytesremaining);
                bytesremaining -= result;

                if (bytesremaining > 0)
                {
                    System.Threading.Thread.Sleep(sleepDuration);
                }
            }

            for (var i = 0; i < count / sizeof(short); i++)
            {
                var sample = BitConverter.ToSingle(kinectBuffer, i * sizeof(float));

                if (sample > 1.0f)
                {
                    sample = 1.0f;
                }
                else if (sample < -1.0f)
                {
                    sample = -1.0f;
                }

                var convertedSample = Convert.ToInt16(sample * short.MaxValue);
                var local = BitConverter.GetBytes(convertedSample);
                System.Buffer.BlockCopy(local, 0, buffer, offset + (i * sizeof(short)), sizeof(short));
            }

            return count;
        }
    }
}
