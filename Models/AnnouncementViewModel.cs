using System.ComponentModel.DataAnnotations;

namespace mvc_web.Models;

public class AnnouncementViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Duyuru başlığı boş bırakılamaz.")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Duyuru içeriği boş bırakılamaz.")]
    public string Content { get; set; } = "";

    public string Author { get; set; } = "Admin";

    [Required(ErrorMessage = "Hedef kitle seçilmelidir.")]
    public string Target { get; set; } = "Tüm Okul";
}