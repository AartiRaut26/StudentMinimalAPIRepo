using System.ComponentModel.DataAnnotations;

namespace MinimalApiStudent.Models
{
    public class Student
    {
        [Key]
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public List<Attendance> Attendances { get; set; }
    }
}
