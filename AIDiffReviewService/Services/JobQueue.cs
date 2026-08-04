using System.Threading.Channels;

namespace AIDiffReviewService.Services
{
    public class JobQueue
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

        public void Enqueue(string jobId) => _channel.Writer.TryWrite(jobId);
        public ChannelReader<string> Reader => _channel.Reader;
    }
}
