using System;
using System.IO;

namespace CanyonEcho_Wpf.Helper
{
    public class ProgressFileStream : FileStream
    {
        public ProgressFileStream(string path, bool isRead)
            : base(path, isRead ? FileMode.Open : FileMode.Create, isRead ? FileAccess.Read : FileAccess.Write)
        {
        }

        public long CurrentSize { get; private set; }
        public event EventHandler<FileEventArgs> Progress;

        public override void Write(byte[] array, int offset, int count)
        {
            base.Write(array, offset, count);
            CurrentSize += count;
            Progress?.Invoke(this, new FileEventArgs(CurrentSize, Length));
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int res = base.Read(array, offset, count);
            CurrentSize += res;
            Progress?.Invoke(this, new FileEventArgs(CurrentSize, Length));
            return res;
        }
    }

    public class FileEventArgs : EventArgs
    {
        public FileEventArgs(long currentSize, long totalSize)
        {
            CurrentSize = currentSize;
            TotalSize = totalSize;
        }

        public long CurrentSize { get; }
        public long TotalSize { get; }
    }
}
