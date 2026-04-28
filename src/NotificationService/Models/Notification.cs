namespace NotificationService.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // Email, SMS, Push
        public string Recipient { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Pending, Sent, Failed, Read
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
