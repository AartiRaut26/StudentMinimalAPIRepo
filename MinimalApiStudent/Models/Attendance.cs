using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinimalApiStudent.Models
{
    public class Attendance
    {
        [Key]

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

        public int AttendanceId { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public DateTime Time { get; set; }
        public Student Student { get; set; }
        public Class Class { get; set; }





    }
}
