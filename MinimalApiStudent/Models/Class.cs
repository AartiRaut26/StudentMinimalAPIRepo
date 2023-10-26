using System.ComponentModel.DataAnnotations;

namespace MinimalApiStudent.Models
{
    public class Class
    {
        [Key]
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        public List<Attendance> Attendances { get; set; }

    }
}
