﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data;
using static AbetApi.Models.AbetModels;

namespace AbetApi.Data
{
    public class AbetRepo : IAbetRepo
    {
        int currentYear = 2021;
        string currentSemester = "spring";

        private string cs = 
        //@"Server=TEBA-D\ABETDATABASE;Database=abetdb11;Trusted_Connection=True";              // <-- Server for RemoteDesktop
        @"Server=TRICO-SCHOOL\SQLEXPRESS;Database=abetdb;Trusted_Connection=True";              // <-- Yafet's local DB
        //@"Server=DESKTOP-5BU0BPP;Database=abetdb;Trusted_Connection=True";                    // <-- Rafael's DB for testing
        //@"Server=LAPTOP-838TO9CN\SQLEXPRESS;Database=abetdb;Trusted_Connection=True";         // <-- Emmanuelli's local DB

        public AbetRepo()
        {

        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(cs);
        }

        public string GetRole(string name)  // change name to userid later
        {
            if (name == null) return "";
            using (SqlConnection conn = GetConnection())
            {
                conn.Open();
                string[] roles = { "Instructor", "Coordinator", "Admin" };
                string query = @"select role from faculty where name = @name";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.VarChar, 50)).Value = name;
                try
                {
                    int obj = Convert.ToInt32(cmd.ExecuteScalar());
                    if (obj != 0) return roles[obj - 1];
                    return "";
                }
                catch
                {
                    return "";
                }
            }
        }

        public List<Section> GetSectionsByUserId(string userId, int year, string term)
        {
            List<Section> secList = new List<Section>();

            string query =
                @"select distinct f.first_name as 'i_firstname', f.last_name as 'i_lastname', f.euid as 'i_euid',
		fa.first_name as 'c_firstname', fa.last_name as 'c_lastname', fa.euid as 'c_euid',
		s.completed as 'sectionCompleted', s.num_of_students as 'NumberOfStudents', s.section_number as 'sectionNumber',
		c.display_name as 'displayName', c.course_number as 'courseNumber', c.completed as 'courseComplete', c.coordinator_comment as 'c_comment',
		c.department as 'department',c.id as id, s.id as section_id
from sections s
inner join courses c on s.course_id = c.id
inner join faculties f on s.instructor_id = f.euid
inner join faculties fa on c.coordinator_id = fa.euid
where (s.instructor_id = @euid or c.coordinator_id = @euid) and c.semester = @term and c.year = @year and c.status = 1";
            SqlConnection conn = GetConnection();
            conn.Open();
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@euid", SqlDbType.VarChar, 50)).Value = userId;
            cmd.Parameters.Add(new SqlParameter("@term", SqlDbType.VarChar, 50)).Value = term;
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = year;
            cmd.Prepare();
            using SqlDataReader rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                Section s = new Section
                {
                    Coordinator = new Coordinator(rd["c_firstname"].ToString(), rd["c_lastname"].ToString(),
                        rd["c_euid"].ToString()),
                    Instructor = new Instructor(rd["i_firstname"].ToString(), rd["i_lastname"].ToString(),
                        rd["i_euid"].ToString()),
                    SectionId = Convert.ToInt32(rd["section_id"]),
                    //Id = Convert.ToInt32(rd["id"]),   delete id from query
                    CoordinatorComment = rd["c_comment"].ToString(),
                    CourseNumber = (rd["courseNumber"]).ToString(),
                    Department = rd["department"].ToString(),
                    DisplayName = rd["displayName"].ToString(),
                    IsCourseCompleted = Convert.ToBoolean(rd["courseComplete"]),
                    IsSectionCompleted = Convert.ToBoolean(rd["sectionCompleted"]),
                    //NumberOfStudents = Convert.ToInt32(rd["NumberOfStudents"]),
                    NumberOfStudents = rd["NumberOfStudents"] as int? ?? 0,
                    SectionNumber = (rd["sectionNumber"]).ToString(),
                    Year = year,
                    Semester = term
                };
                secList.Add(s);
            }
            conn.Close();
            return secList;
        }

        public Program_Outcomes GetCourseObjectives(string program)
        {
            Program_Outcomes program_outcomes;
            Course_Objective course_objectives = null;
            CourseMapping course_outcome;
            Student_Outcome student_outcome;
            List<Student_Outcome> student_Outcomes = new List<Student_Outcome>();
            List<CourseMapping> course_Outcomes = new List<CourseMapping>();
            List<Course_Objective> courseObjectives = new List<Course_Objective>();
            string displayName = null;

            string query1 = @"select co.num as 'order', co.course_outcome as outcome, cob.student_outcome_mapping, c.display_name from 
course_objectives as cob inner join course_outcomes as co on co.id = cob.course_outcome_id 
inner join courses as c on c.id = cob.course_id where c.department = 'csce' and c.status = 1";
            SqlConnection conn = GetConnection();
            conn.Open();
            SqlCommand cmd = new SqlCommand(query1, conn);
            cmd.Prepare();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    course_outcome = new CourseMapping
                    {
                        Order = Convert.ToInt32(rd["order"]),
                        Outcome = rd["outcome"].ToString(),
                        MappedStudentOutcomes = (rd["student_outcome_mapping"].ToString()).Select(c => int.Parse(c.ToString())).ToArray()
                    };
                    if (displayName == null) displayName = rd["display_name"].ToString();
                    else if (displayName != rd["display_name"].ToString())
                    {
                        course_objectives = new Course_Objective(displayName, course_Outcomes);
                        courseObjectives.Add(course_objectives);
                        displayName = rd["display_name"].ToString();
                        course_Outcomes = new List<CourseMapping>();

                    }
                    course_Outcomes.Add(course_outcome);
                }
            }
            course_objectives = new Course_Objective(displayName, course_Outcomes);
            courseObjectives.Add(course_objectives);

            query1 = @"select st.num, st.student_outcome, p.program from student_outcomes as 
st join programs as p on p.id = st.program_id where p.program = @program";
            cmd = new SqlCommand(query1, conn);
            cmd.Parameters.Add(new SqlParameter("@program", SqlDbType.VarChar, 50)).Value = program;
            cmd.Prepare();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    student_outcome = new Student_Outcome
                    {
                        Order = Convert.ToInt32(rd["num"]),
                        Outcome = rd["student_outcome"].ToString()
                    };
                    student_Outcomes.Add(student_outcome);
                }
            }

            conn.Close();
            program_outcomes = new Program_Outcomes(program, courseObjectives, student_Outcomes);
            return program_outcomes;
        }

        public List<Course> GetCoursesByDepartment(string department)
        {
            List<Course> coursesList = new List<Course>();
            Course course;
            string query = @"select c.course_number, f.first_name, f.last_name, f.euid, c.course_number, c.display_name, c.coordinator_comment, 
c.completed as IsCourseCompleted, c.department, c.id from courses as c 
left join faculties as f on c.coordinator_id = f.euid
where c.department = @department and c.year = @year and c.semester = @semester and c.status = 1";

            SqlConnection conn = GetConnection();
            conn.Open();
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 50)).Value = department;
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = currentYear;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 50)).Value = currentSemester;
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    course = new Course
                    {
                        Coordinator = new Coordinator(rd["first_name"].ToString(), rd["last_name"].ToString(), rd["euid"].ToString()),
                        CourseNumber = (rd["course_number"]).ToString(),
                        DisplayName = rd["display_name"].ToString(),
                        //Id = Convert.ToInt32(rd["id"]),   // REMOVE ID FROM QUERY
                        CoordinatorComment = rd["coordinator_comment"].ToString(),
                        IsCourseCompleted = Convert.ToBoolean(rd["IsCourseCompleted"]),
                        Department = department,
                        Year = currentYear,
                        Semester = currentSemester
                    };
                    coursesList.Add(course);
                }
            }

            return coursesList;
        }

        public bool AddCourse(Course course)
        {
            string insertQuery = @"insert into courses (year, semester, department, course_number, coordinator_id, display_name)
values (@year,@semester, @department, @course_number, @coordinator_id, @display_name)";

            SqlConnection conn = GetConnection();
            conn.Open();

            SqlCommand cmd = new SqlCommand(insertQuery, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = course.Year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 50)).Value = course.Semester;
            cmd.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 50)).Value = course.Department;
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 50)).Value = course.CourseNumber;
            cmd.Parameters.Add(new SqlParameter("@coordinator_id", SqlDbType.VarChar, 15)).Value = course.Coordinator.Id;
            cmd.Parameters.Add(new SqlParameter("@display_name", SqlDbType.VarChar, 50)).Value = course.DisplayName;
            cmd.Prepare();

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool RemoveCourse(Course course)
        {
            string updateQuery = @"update courses set status = 0 where year = @year and semester = @semester 
and department = @department and course_number = @course_number";

            SqlConnection conn = GetConnection();
            conn.Open();

            SqlCommand cmd = new SqlCommand(updateQuery, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = course.Year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 50)).Value = course.Semester;
            cmd.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 50)).Value = course.Department;
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 50)).Value = course.CourseNumber;
            cmd.Prepare();

            return cmd.ExecuteNonQuery() > 0;
        }

        public FacultyList GetFacultyList()
        {
            using (SqlConnection conn = GetConnection())
            {
                FacultyList facultyList = new FacultyList();
                Instructor instructor;
                Info info = new Info();

                string selectQuery = @"select euid as id, first_name, last_name, faculty_type from faculties";
                SqlCommand cmd = new SqlCommand(selectQuery, conn);
                cmd.Connection.Open();
                try
                {
                    using (SqlDataReader rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            instructor = new Instructor
                            {
                                FirstName = rd["first_name"].ToString(),
                                LastName = rd["last_name"].ToString(),
                                Id = rd["id"].ToString()
                            };
                            if (rd["faculty_type"].ToString() == "Full-time")
                                facultyList.FullTime.Add(instructor);
                            else if (rd["faculty_type"].ToString() == "Adjunct")
                            {
                                facultyList.Adjuncts.Add(instructor);
                            }
                            else
                            {
                                facultyList.Fellows.Add(instructor);
                            }
                        }
                    }
                    return facultyList;
                }
                catch
                {
                    return null;
                }
            }  
        }

        public bool AddFacultyMember(Info info, string facultyType)
        {
            using (SqlConnection conn = GetConnection())
            {
                string insertQuery = @"insert into faculties (euid, first_name, last_name, role, faculty_type) 
values (@euid, @first_name, @last_name, @role, @faculty_type)";
                SqlCommand cmd = new SqlCommand(insertQuery, conn);
                cmd.Parameters.Add(new SqlParameter("@euid", SqlDbType.VarChar, 20)).Value = info.Id;
                cmd.Parameters.Add(new SqlParameter("@first_name", SqlDbType.VarChar, 50)).Value = info.FirstName;
                cmd.Parameters.Add(new SqlParameter("@last_name", SqlDbType.VarChar, 50)).Value = info.LastName;
                cmd.Parameters.Add(new SqlParameter("@role", SqlDbType.Int)).Value = 1; // 1 is instructor
                cmd.Parameters.Add(new SqlParameter("@faculty_type", SqlDbType.VarChar, 50)).Value = facultyType;
                cmd.Connection.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                } catch
                {
                    return false;
                }
            }
        }

        public Form GetFormBySection(Section section)
        {
            Form form;
            Grades csGrade = null;
            Grades ceGrade = null;
            Grades itGrade = null;
            StudentWork studentWork;
            List<StudentWork> studentWorks = new List<StudentWork>();
            OutcomeObjective outcomeObjective = null;
            List<OutcomeObjective> outcomeObjectives = new List<OutcomeObjective>();
            int tempID = 0;

            string selectQuery = @"select so.id, so.section_id, 
it.a as it_a, it.b as it_b, it.c as it_c, it.d as it_d, it.f as it_f, it.w as it_w, it.i as it_i, 
(it.a + it.b + it.c + it.d + it.f + it.w + it.i) as it_total, 
cs.a as cs_a, cs.b as cs_b, cs.c as cs_c, cs.d as cs_d, cs.f as cs_f, cs.w as cs_w, cs.i as cs_i, 
(cs.a + cs.b + cs.c + cs.d + cs.f + cs.w + cs.i) as cs_total, 
ce.a as ce_a, ce.b as ce_b, ce.c as ce_c, ce.d as ce_d, ce.f as ce_f, ce.w as ce_w, ce.i as ce_i, 
(ce.a + ce.b + ce.c + ce.d + ce.f + ce.w + ce.i) as ce_total
from section_objectives as so join sections as s on s.id = so.section_id
join grades as it on so.IT_grade_id = it.id
join grades as cs on so.CS_grade_id = cs.id
join grades as ce on so.CE_grade_id = ce.id
join courses as c on c.id = s.course_id
where c.year = @year and c.semester = @semester and c.course_number = @course_number and s.section_number = @section_number";

            SqlConnection conn = GetConnection();
            conn.Open();
            SqlCommand cmd = new SqlCommand(selectQuery, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = section.Year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 20)).Value = section.Semester;
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 20)).Value = section.CourseNumber;
            cmd.Parameters.Add(new SqlParameter("@section_number", SqlDbType.VarChar, 20)).Value = section.SectionNumber;
            cmd.Prepare();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                if (rd.Read())
                {
                    csGrade = new Grades(
                        Convert.ToInt32(rd["cs_a"]),
                        Convert.ToInt32(rd["cs_b"]),
                        Convert.ToInt32(rd["cs_c"]),
                        Convert.ToInt32(rd["cs_d"]),
                        Convert.ToInt32(rd["cs_f"]),
                        Convert.ToInt32(rd["cs_w"]),
                        Convert.ToInt32(rd["cs_i"]),
                        Convert.ToInt32(rd["cs_total"])
                        );
                    itGrade = new Grades(
                        Convert.ToInt32(rd["it_a"]),
                        Convert.ToInt32(rd["it_b"]),
                        Convert.ToInt32(rd["it_c"]),
                        Convert.ToInt32(rd["it_d"]),
                        Convert.ToInt32(rd["it_f"]),
                        Convert.ToInt32(rd["it_w"]),
                        Convert.ToInt32(rd["it_i"]),
                        Convert.ToInt32(rd["it_total"])
                        );
                    ceGrade = new Grades(
                        Convert.ToInt32(rd["ce_a"]),
                        Convert.ToInt32(rd["ce_b"]),
                        Convert.ToInt32(rd["ce_c"]),
                        Convert.ToInt32(rd["ce_d"]),
                        Convert.ToInt32(rd["ce_f"]),
                        Convert.ToInt32(rd["ce_w"]),
                        Convert.ToInt32(rd["ce_i"]),
                        Convert.ToInt32(rd["ce_total"])
                        );
                    section.SectionId = Convert.ToInt32(rd["section_id"]);
                }
                else
                {
                    return GetBlankForm(section);
                }
            }

            selectQuery = @"select ou.file_name, ou.fileupload, ou.id, oo.num_of_CE, oo.num_of_CS, num_of_IT, 
co.course_outcome as outcome, co.num, co.id as outcome_id, s.section_number
from objective_uploads as ou 
left join outcome_objectives as oo on oo.id = ou.outcome_objective_id
join course_outcomes as co on co.id = oo.outcome_id
join courses as c on c.id = co.course_id
join sections as s on s.course_id = c.id
where c.year = @year and c.semester = @semester and c.course_number = @course_number and s.section_number = @section_number";

            form = new Form(section, outcomeObjectives, itGrade, csGrade, ceGrade);
            cmd = new SqlCommand(selectQuery, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = section.Year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 20)).Value = section.Semester;
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 20)).Value = section.CourseNumber;
            cmd.Parameters.Add(new SqlParameter("@section_number", SqlDbType.VarChar, 20)).Value = section.SectionNumber;
            cmd.Prepare();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    studentWork = new StudentWork
                    {
                        //Id = Convert.ToInt32(rd["id"]),   REMOVE ID FROM QUERY
                        FileName = rd["file_name"].ToString(),
                        FileUploaded = rd["fileupload"].ToString()
                    };
                    if (tempID != Convert.ToInt32(rd["num"]))
                    {
                        if (tempID != 0)
                        {
                            outcomeObjective.StudentWorks = studentWorks;
                            outcomeObjectives.Add(outcomeObjective);
                            studentWorks = new List<StudentWork>();
                        }

                        outcomeObjective = new OutcomeObjective
                        {
                            Outcome = rd["outcome"].ToString(),
                            OrderOfOutcome = Convert.ToInt32(rd["num"]),
                            //OutcomeId = Convert.ToInt32(rd["outcome_id"]),    REMOVE ID FROM QUERY
                            NumberOfCE = Convert.ToInt32(rd["num_of_CE"]),
                            NumberOfCS = Convert.ToInt32(rd["num_of_CS"]),
                            NumberOfIT = Convert.ToInt32(rd["num_of_IT"]),
                            StudentWorks = studentWorks
                        };
                        tempID = Convert.ToInt32(rd["num"]);
                    }
                    studentWorks.Add(studentWork);
                }
                outcomeObjective.StudentWorks = studentWorks;
                outcomeObjectives.Add(outcomeObjective);
            }
            form = new Form(section, outcomeObjectives, itGrade, csGrade, ceGrade);
            // if form doesnt exist, get blank form
            return form;
        }
        // WORK ON  THIS ONE . MAKE IT SEARUCH USING COURSE INFO
        public Form GetBlankForm(Section section)
        {
            Form toReturn = new Form();
            List<OutcomeObjective> outcomeObjectives = new List<OutcomeObjective>();
            List<StudentWork> studentWorks = new List<StudentWork>();

            string query = @"select num, course_outcome, id from course_outcomes where course_id = @course_id";
            SqlConnection conn = GetConnection();
            conn.Open();
            SqlCommand cmd = new SqlCommand(query, conn);
            //cmd.Parameters.Add(new SqlParameter("@course_id", SqlDbType.Int)).Value = section.Id;

            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    OutcomeObjective outcomeObjective = new OutcomeObjective
                    {
                        OrderOfOutcome = Convert.ToInt32(rd["num"]),
                        Outcome = rd["course_outcome"].ToString(),
                        //OutcomeId = Convert.ToInt32(rd["id"]),
                        StudentWorks = studentWorks,
                        NumberOfIT = 0,
                        NumberOfCE = 0,
                        NumberOfCS = 0
                    };
                    outcomeObjectives.Add(outcomeObjective);
                }
            }

            toReturn.CEGrades = new Grades(0, 0, 0, 0, 0, 0, 0, 0);
            toReturn.CSGrades = new Grades(0, 0, 0, 0, 0, 0, 0, 0);
            toReturn.ITGrades = new Grades(0, 0, 0, 0, 0, 0, 0, 0);
            toReturn.Outcomes = outcomeObjectives;
            toReturn.Section = section;

            conn.Close();
            return toReturn;
        }
        public SqlReturn PostForm(Form form)
        {
            int sec_obj, out_obj, uploadID;
            SqlReturn sqlReturn = new SqlReturn();
            int gradeID;
            int i, k;
            string query1;
            //int first = 0, total = 0;
            // configure connection
            SqlConnection conn = GetConnection();
            conn.Open();
            
            // test for variables
            if ( (form == null) || (form.Section == null) || (form.Outcomes == null) )
            {
                sqlReturn.code = -1;
                sqlReturn.message = "Form and/or Section are missing. Both must be provided";
                return sqlReturn;
            }
            else if (String.IsNullOrEmpty(form.Section.SectionNumber))
            {
                sqlReturn.code = -1;
                sqlReturn.message = "SectionNumber is missing";
                return sqlReturn;
            }

            // confirm section exists
            string query = @"select s.id 
from sections as s join courses as c on c.id = s.course_id
where c.year = @year and c.semester = @semester and c.course_number = @course_number and s.section_number = @section_number";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = form.Section.Year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = form.Section.Semester;
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 11)).Value = form.Section.CourseNumber;
            cmd.Parameters.Add(new SqlParameter("@section_number", SqlDbType.VarChar, 11)).Value = form.Section.SectionNumber;
            Object s = cmd.ExecuteScalar();

            if (!(s is DBNull))
            {
                int n;
                n = Convert.ToInt32(s);
                form.Section.SectionId = n;
            }
            else
            {
                sqlReturn.message = $"Course {form.Section.Department} {form.Section.CourseNumber} or Section {form.Section.SectionNumber} does not exist";
                sqlReturn.code = -1;
                return sqlReturn;
            }

            // insert section_objectives (first check if it exist)
            query = $"select id from section_objectives where section_id = @section_id";
            cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@section_id", SqlDbType.Int)).Value = form.Section.SectionId;
            sec_obj = Convert.ToInt32(cmd.ExecuteScalar());
            if (sec_obj == 0)
            {
                query = @"insert into section_objectives (section_id) Values (@section_id); SELECT SCOPE_IDENTITY()";
                cmd = new SqlCommand(query, conn);
                cmd.Parameters.Add(new SqlParameter("@section_id", SqlDbType.Int)).Value = form.Section.SectionId;
                try
                {
                    sec_obj = Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch (Exception ex)
                {
                    sqlReturn.message = ex.Message;
                    sqlReturn.code = -1;
                    return sqlReturn;
                }
            }

            // insert grades
            //List<Grades> grades = new List<Grades> { form.CEGrades, form.CSGrades, form.ITGrades };
            Grades[] grades = { form.CEGrades, form.CSGrades, form.ITGrades };
            string[] arr = { "CE_grade_id", "CS_grade_id", "IT_grade_id" };
            for (i = 0; i < 3; i++)
            {
                query = $"select {arr[i]} from section_objectives where section_id = {form.Section.SectionId}";
                cmd = new SqlCommand(query, conn);
                k = Convert.ToInt32(cmd.ExecuteScalar());
                if (k == 0)
                {
                    query = @"insert into grades (a, b, c, d, f, w, i, section_objective_id) 
VALUES (@a, @b, @c, @d, @f, @w, @i, @section_objective_id); SELECT SCOPE_IDENTITY()";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.Add(new SqlParameter("@a", SqlDbType.Int)).Value = grades[i].A;                  
                    cmd.Parameters.Add(new SqlParameter("@b", SqlDbType.Int)).Value = grades[i].B;
                    cmd.Parameters.Add(new SqlParameter("@c", SqlDbType.Int)).Value = grades[i].C;
                    cmd.Parameters.Add(new SqlParameter("@d", SqlDbType.Int)).Value = grades[i].D;
                    cmd.Parameters.Add(new SqlParameter("@f", SqlDbType.Int)).Value = grades[i].F;
                    cmd.Parameters.Add(new SqlParameter("@w", SqlDbType.Int)).Value = grades[i].W;
                    cmd.Parameters.Add(new SqlParameter("@i", SqlDbType.Int)).Value = grades[i].I;
                    cmd.Parameters.Add(new SqlParameter("@section_objective_id", SqlDbType.Int)).Value = sec_obj;
                    try
                    {
                        gradeID = Convert.ToInt32(cmd.ExecuteScalar());
                        query1 = $"update section_objectives set {arr[i]} = {gradeID}";
                        cmd = new SqlCommand(query1, conn);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        sqlReturn.message = ex.Message;
                        sqlReturn.code = -1;
                        return sqlReturn;
                    }
                }
                else
                {
                    query = @"update abetdb.dbo.grades SET a = @a, b = @b, c = @c, d = @d, f = @f, w = @w, i = @i where id = @id";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.Add(new SqlParameter("@a", SqlDbType.Int)).Value = grades[i].A;
                    cmd.Parameters.Add(new SqlParameter("@b", SqlDbType.Int)).Value = grades[i].B;
                    cmd.Parameters.Add(new SqlParameter("@c", SqlDbType.Int)).Value = grades[i].C;
                    cmd.Parameters.Add(new SqlParameter("@d", SqlDbType.Int)).Value = grades[i].D;
                    cmd.Parameters.Add(new SqlParameter("@f", SqlDbType.Int)).Value = grades[i].F;
                    cmd.Parameters.Add(new SqlParameter("@w", SqlDbType.Int)).Value = grades[i].W;
                    cmd.Parameters.Add(new SqlParameter("@i", SqlDbType.Int)).Value = grades[i].I;
                    cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int)).Value = k;
                    cmd.ExecuteNonQuery();
                }
            }

            /* ***************************************  Part 2   *********************************************/

            // insert outcome objectives (first check if exist)
            foreach (OutcomeObjective outcomeObjective in form.Outcomes)
            {
                query = $"select id from outcome_objectives where outcome_id = {outcomeObjective.OutcomeId}";
                cmd = new SqlCommand(query, conn);
                out_obj = Convert.ToInt32(cmd.ExecuteScalar());
                if (out_obj == 0)
                {
                    query = @"insert into outcome_objectives (outcome_id, num_of_CE, num_of_CS, num_of_IT, section_id)
values (@outcome_id, @num_of_CE, @num_of_CS, @num_of_IT, @section_id); SELECT SCOPE_IDENTITY()";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.Add(new SqlParameter("@outcome_id", SqlDbType.Int)).Value = outcomeObjective.OutcomeId;
                    cmd.Parameters.Add(new SqlParameter("@num_of_CE", SqlDbType.Int)).Value = outcomeObjective.NumberOfCE;
                    cmd.Parameters.Add(new SqlParameter("@num_of_CS", SqlDbType.Int)).Value = outcomeObjective.NumberOfCS;
                    cmd.Parameters.Add(new SqlParameter("@num_of_IT", SqlDbType.Int)).Value = outcomeObjective.NumberOfIT;
                    cmd.Parameters.Add(new SqlParameter("@section_id", SqlDbType.Int)).Value = form.Section.SectionId;
                    try
                    {
                        out_obj = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    catch (Exception ex)
                    {
                        sqlReturn.message = ex.Message;
                        sqlReturn.code = -1;
                        return sqlReturn;
                    }
                }
                foreach (StudentWork studentWork in outcomeObjective.StudentWorks)
                {
                    query = @"INSERT into objective_uploads (file_name, fileupload, outcome_objective_id)
select @file_name, @fileupload, @outcome_objective_id
where NOT EXISTS (select file_name, fileupload, outcome_objective_id from objective_uploads where id = @id); SELECT SCOPE_IDENTITY()";
                    cmd = new SqlCommand(query, conn);
                    cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int)).Value = studentWork.id;
                    cmd.Parameters.Add(new SqlParameter("@outcome_objective_id", SqlDbType.Int)).Value = out_obj;
                    cmd.Parameters.Add(new SqlParameter("@file_name", SqlDbType.VarChar, 100)).Value = studentWork.FileName;
                    cmd.Parameters.Add(new SqlParameter("@fileupload", SqlDbType.VarChar, 20)).Value = studentWork.FileUploaded;
                    try
                    {
                        Object obj = cmd.ExecuteScalar();
                        if (!(obj is DBNull))
                        {
                            uploadID = Convert.ToInt32(obj);
                            studentWork.id = uploadID;
                        }
                    }
                    catch (Exception ex)
                    {
                        sqlReturn.message = ex.Message;
                        sqlReturn.code = -1;
                        return sqlReturn;
                    }
                }
            }

            conn.Close();
            return sqlReturn;
        }
        public List<Form> GetFormsByCourse(Course course)
        {
            List<Form> forms = new List<Form>();
            if (course.CourseNumber == null || course.Department == null || course.Year == null || course.Department == null) return forms;

            SqlConnection conn = GetConnection();
            conn.Open();
            string query = @"select c.id as c_id, fa.first_name as c_first_name, fa.last_name as c_last_name, fa.euid as c_euid,
c.course_number, c.display_name, c.coordinator_comment, c.completed as c_completed, c.department, c.year, c.semester,
s.id as s_id, f.first_name as i_first_name, f.last_name as i_last_name, f.euid as i_euid, s.completed as s_completed, s.section_number,
s.num_of_students 
from abetdb.dbo.sections as s join abetdb.dbo.courses as c on s.course_id = c.id
join abetdb.dbo.faculties as f on s.instructor_id = f.euid 
join abetdb.dbo.faculties as fa on c.coordinator_id = fa.euid
where c.year = @year and c.semester = @semester and c.course_number = @course_number and c.department = @department and c.status = 1";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 11)).Value = course.CourseNumber;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = course.Semester;
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = course.Year;
            cmd.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 11)).Value = course.Department;

            Form form = new Form(); // section, list<outcomeobjective> , grades IT, CS, CE 
            List<Section> sections = new List<Section>();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    Section section = new Section
                    {
                        Coordinator = new Coordinator(rd["c_first_name"].ToString(), rd["c_last_name"].ToString(),
                        rd["c_euid"].ToString()),
                        Instructor = new Instructor(rd["i_first_name"].ToString(), rd["i_last_name"].ToString(),
                        rd["i_euid"].ToString()),
                        SectionId = Convert.ToInt32(rd["s_id"]),
                        //Id = Convert.ToInt32(rd["c_id"]), REMOVE ID FROM QUERY
                        CoordinatorComment = rd["coordinator_comment"].ToString(),
                        CourseNumber = (rd["course_number"]).ToString(),
                        Department = rd["department"].ToString(),
                        DisplayName = rd["display_name"].ToString(),
                        IsCourseCompleted = Convert.ToBoolean(rd["c_completed"]),
                        IsSectionCompleted = Convert.ToBoolean(rd["s_completed"]),
                        NumberOfStudents = rd["num_of_students"] as int? ?? 0,
                        SectionNumber = (rd["section_number"]).ToString(),
                        Year = course.Year,
                        Semester = course.Semester
                    };
                    sections.Add(section);
                }
            }
            // check if empty
            if (sections.Count == 0) return forms;

            foreach (Section s in sections)
            {
                form = GetFormBySection(s);
                forms.Add(form);
            }

            conn.Close();
            return forms;
        }

        public List<Section> GetSectionsByYearAndSemester(int year, string semester)
        {
            List<Section> sectionList = new List<Section>();   // Return variable

            /* Establish and open connection */
            SqlConnection conn = GetConnection();           // Establish the connection
            conn.Open();                                    // Open the connection
            string query = @"SELECT c.id AS c_id, fa.first_name AS c_first_name, fa.last_name AS c_last_name, fa.euid AS c_euid, c.course_number, c.display_name, c.coordinator_comment, c.completed AS c_completed, c.department, c.year, c.semester, s.id AS s_id, f.first_name AS i_first_name, f.last_name AS i_last_name, f.euid AS i_euid, s.completed AS s_completed, s.section_number, s.num_of_students FROM abetdb.dbo.sections AS s JOIN abetdb.dbo.courses AS c ON s.course_id = c.id JOIN abetdb.dbo.faculties AS f ON s.instructor_id = f.euid JOIN abetdb.dbo.faculties AS fa ON c.coordinator_id = fa.euid WHERE c.year = @year AND c.semester = @semester AND c.status = 1";
               /* ^^ NOTES ^^
                        - "abetdb.dbo." removed from table names here because the database won't need to be specified with UNT's server.
                        - c.year = 2021 AND c.semester = 'spring'  ==> c.year = @year AND c.semester = @semester  | The '@' at the beginning of the string allows '@year' and '@semester' to pass in the parameters 'year' and 'semester' passed in through the function call.           
               */
            
            SqlCommand cmd = new SqlCommand(query, conn);                                               // Create SQL Command
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = year;                  // Add the 'year' parameter for GetSectionsByYearAndSemester() as a parameter for the SQL Command as '@year' and specified at an 'int' type
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = semester;  // Add the 'semester' parameter for GetSectionsByYearAndSemester() as a parameter for the SQL Command as '@semester', specified at a 'varchar' type, of size 11 (size based on 'semester' row of 'courses' table)
            cmd.Prepare();                                                                              // Prepare the command to be run

            /* Read in incoming data */
            using (SqlDataReader rd = cmd.ExecuteReader())  // Reads data coming in
            {       
                /* Loops through one row at a time */
                while (rd.Read())
                {
                    /* Assign Section-specific data */
                    var section = new Section()                                                                                                 // Create new Section object
                    {
                        SectionId = Convert.ToInt32(rd["s_id"]),                                                                                // Convert and add incoming section id
                        Coordinator = new Coordinator(rd["c_first_name"].ToString(), rd["c_last_name"].ToString(), rd["c_euid"].ToString()),    // Convert and add incoming Coordinator information
                        Instructor = new Instructor(rd["i_first_name"].ToString(), rd["i_last_name"].ToString(), rd["i_euid"].ToString()),      // Convert and add incoming Instructor information
                        CourseNumber = rd["course_number"].ToString(),                                                                          // Convert and add the incoming course number
                        //Id = Convert.ToInt32(rd["c_id"]), REMOVE ID FROM QUERY                                                                // Convert and add incoming Identity id
                        CoordinatorComment = rd["coordinator_comment"].ToString(),                                                              // Convert and add incoming coordinator comment
                        Department = rd["department"].ToString(),                                                                                // Convert and add incoming department
                        DisplayName = rd["display_name"].ToString(),                                                                            // Convert and add incoming display name
                        IsCourseCompleted = Convert.ToBoolean(rd["c_completed"]),                                                               // Convert and add incoming boolean for IsCourseCompleted
                        IsSectionCompleted = Convert.ToBoolean(rd["s_completed"]),                                                              // Convert and add incoming boolean for IsSectionCompleted
                        NumberOfStudents = Convert.ToInt32(rd["num_of_students"]),                                                              // Convert and add incoming number of students for the section
                        SectionNumber = rd["section_number"].ToString(),                                                                        // Convert and add incoming section number
                        Year = year,                                                                                                            // Assign 'year' parameter from GetSectionsByYearAndSemester() for Year
                        Semester = semester                                                                                                     // Assign 'semester' parameter from GetSectionsByYearAndSemester() for Semester
                    };
                    sectionList.Add(section);                                                                                                   // Add the retrieved object to the collection
                }
            }

            conn.Close();           // Close the connection
            return sectionList;     // Return the section list
        }
        public List<Course> GetCoursesByYear(int year, string semester)
        {
            List<Course> courses = new List<Course>();
            if (year < 0 || semester == null) return courses;

            SqlConnection conn = GetConnection();
            conn.Open();
            string query = @"select c.department, c.course_number, c.coordinator_comment, c.completed, c.display_name, 
f.first_name, f.last_name, f.euid
from courses as c 
join faculties as f on c.coordinator_id = f.euid
where c.year = @year and c.semester = @semester and c.status = 1";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = year;
            cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = semester;

            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    Course course = new Course
                    {
                        Coordinator = new Coordinator(rd["first_name"].ToString(), rd["last_name"].ToString(),
                        rd["euid"].ToString()),
                        CourseNumber = rd["course_number"].ToString(),
                        DisplayName = rd["display_name"].ToString(),
                        Department = rd["department"].ToString(),
                        CoordinatorComment = rd["coordinator_comment"].ToString(),
                        IsCourseCompleted = Convert.ToBoolean(rd["completed"]),
                        Year = year,
                        Semester = semester,
                    };
                    courses.Add(course);
                }
            }

            conn.Close();
            return courses;
        }

        /* Allows instructors to add a new section. */
        public bool PostSection(Section section)
        {
            /* 0 == false | 1 == true */
            int sectionCompletion;          // Used to determine the value to which section.IsSectionCompleted associates with
            if (section.IsSectionCompleted) 
            { 
                sectionCompletion = 1; 
            }
            else
            {
                sectionCompletion = 0;
            }

            /* Query for getting the courseID based on the department, course number, semester, and year */
            string courseId_Query = @"SELECT id FROM [abetdb].[dbo].[courses] WHERE department = @department AND course_number = @course_number AND semester = @semester AND year = @year;";
            
            SqlConnection conn = GetConnection();               // Establish connection with the DB
            conn.Open();                                        // Open the connection
            
            /* Create the command and add values to parameters */
            SqlCommand id_cmd = new SqlCommand(courseId_Query, conn);
            id_cmd.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 11)).Value = section.Department;
            id_cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 11)).Value = section.CourseNumber;
            id_cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = section.Semester;
            id_cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = section.Year;
            id_cmd.Prepare();

            Int32 courseId = (Int32)id_cmd.ExecuteScalar();     // Execute command for the courseID

            /* Query for creating/inserting a new section */
            string query = @"INSERT INTO [abetdb].[dbo].[sections] (course_id, section_number, instructor_id, num_of_students, completed) VALUES (@courseID, @sectionNum, @instructorID, @numStudents, @completion);";

            /* Create the command and add values to parameters */
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@courseID", SqlDbType.VarChar, 10)).Value = courseId.ToString();
            cmd.Parameters.Add(new SqlParameter("@sectionNum", SqlDbType.VarChar, 11)).Value = section.SectionNumber;
            cmd.Parameters.Add(new SqlParameter("@instructorID", SqlDbType.VarChar, 11)).Value = section.Instructor.Id;
            cmd.Parameters.Add(new SqlParameter("@numStudents", SqlDbType.Int)).Value = section.NumberOfStudents;
            cmd.Parameters.Add(new SqlParameter("@completion", SqlDbType.SmallInt)).Value = sectionCompletion;
            cmd.Prepare();

            /* Excute the query and return the status */
            if (cmd.ExecuteNonQuery() > 0)
            {
                conn.Close();
                return true;
            }
            else
            {
                conn.Close();
                return false;
            }
        }
        public List<CourseMapping> GetCourseOutcomesByCourse(Course course)
        {
            
            List<CourseMapping> result = new List<CourseMapping>();
            SqlConnection conn = GetConnection();
            conn.Open();

            string query = @"SELECT co.num, co.course_outcome, c.display_name " +
                "from course_outcomes as co join courses as c on co.course_id = c.id where c.display_name LIKE @course;";
            SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add(new SqlParameter("@course", SqlDbType.VarChar, 20)).Value = course.DisplayName;

            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    CourseMapping db_result = new CourseMapping
                    {
                        Outcome = rd["course_outcome"].ToString(),
                        Order = Int32.Parse(rd["num"].ToString()),
                    };
                result.Add(db_result);
                }
            }

            conn.Close();
            return result;
        }

        public bool PostComment(Course course)
        {
            if (course == null || course.CoordinatorComment == null || course.Year < 2000 || course.Semester == null || course.CourseNumber == null) return false;

            using (SqlConnection conn = GetConnection())
            {
                string query = @"UPDATE courses 
SET coordinator_comment = @coordinator_comment
WHERE year = @year and semester = @semester and course_number = @course_number and status = 1";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.Add(new SqlParameter("@coordinator_comment", SqlDbType.VarChar, -1)).Value = course.CoordinatorComment;
                cmd.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = course.Year;
                cmd.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = course.Semester;
                cmd.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 11)).Value = course.CourseNumber;
                cmd.Connection.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public SqlReturn PostAccessDbData(string filePath)
        {
            // access database connection
            string dsn = @"Provider=Microsoft.ACE.OLEDB.12.0; Data Source = " + filePath;

            using (OleDbConnection conn = new OleDbConnection(dsn))
            {
                string query = @"SELECT [Course Number] as course_number, [Name of Course] as course_name, Coordinator, Coordinator_ID, 
            [Group CE] as group_ce, [Group CS] as group_cs, [Group IT] as group_it, 
            Outcome1, Outcome2, Outcome3, Outcome4, Outcome5, Outcome6, Outcome7, Outcome8, Outcome9,
            [CE - StudentOutcomes1], [CE - StudentOutcomes2], [CE - StudentOutcomes3], [CE - StudentOutcomes4], [CE - StudentOutcomes5], [CE - StudentOutcomes6], 
            [CE - StudentOutcomes7], [CE - StudentOutcomes8], [CE - StudentOutcomes9], [CE - StudentOutcomes10], [CE - StudentOutcomes11],  [CE - StudentOutcomes12], 
            [CE - StudentOutcomes13], [CE - StudentOutcomes14],
            [CS - StudentOutcomes1], [CS - StudentOutcomes2], [CS - StudentOutcomes3], [CS - StudentOutcomes4], [CS - StudentOutcomes5], [CS - StudentOutcomes6], 
            [CS - StudentOutcomes7], [CS - StudentOutcomes8], [CS - StudentOutcomes9], [CS - StudentOutcomes10], [CS - StudentOutcomes11],  [CS - StudentOutcomes12], 
            [CS - StudentOutcomes13], [CS - StudentOutcomes14],
            [IT - StudentOutcomes1], [IT - StudentOutcomes2], [IT - StudentOutcomes3], [IT - StudentOutcomes4], [IT - StudentOutcomes5], [IT - StudentOutcomes6], 
            [IT - StudentOutcomes7], [IT - StudentOutcomes8], [IT - StudentOutcomes9], [IT - StudentOutcomes10], [IT - StudentOutcomes11],  [IT - StudentOutcomes12], 
            [IT - StudentOutcomes13], [IT - StudentOutcomes14]
            FROM Sheet1";

                using (SqlConnection conn1 = GetConnection())
                {
                    // unless specified, current year and semester will be calculated using current date
                    int current_year = DateTime.Now.Year;
                    string current_department = "csce";
                    string current_semester;
                    int month = DateTime.Now.Month;
                    if (month > 1 && month < 5) current_semester = "spring";
                    else if (month > 8 && month < 1) current_semester = "fall";
                    else current_semester = "summer";

                    //variable declaration
                    int ce, cs, it, status;
                    int i, j, k, t, result;
                    string outcome;
                    string student_outcome;
                    string[] p = { "CE - ", "CS - ", "IT - " };   // add "CYBR - " later
                    string[] programs = { "ce", "cs", "it" };     // add "cybr" later
                    string[] names = null;
                    SqlReturn sqlReturn = new SqlReturn();
                    sqlReturn.code = 1;

                    // sql server connection
                    string query1 = @"INSERT INTO faculties (first_name, last_name, euid, role, faculty_type)
SELECT @first_name, @last_name, @euid, @role, @faculty_type
WHERE NOT EXISTS (SELECT * FROM faculties WHERE euid = @euid); insert into courses (year, semester, department, course_number, coordinator_name, 
coordinator_id, display_name, group_ce, group_cs, group_it, status)
values 
(@year, @semester, @department, @course_number, @coordinator_name, @coordinator_id, @display_name, @group_ce, @group_cs, @group_it, @status); 
SELECT SCOPE_IDENTITY()";
                    string query2 = @"insert into course_outcomes (num, course_outcome, course_id)
values (@num, @course_outcome, @course_id)";
                    string query3 = @"insert into course_objective (course_id, program, student_outcome_order, course_outcome_order) 
VALUES (@course_id, @program, @student_outcome_order, @course_outcome_order)";

                    SqlCommand cmd1;
                    try
                    {
                        OleDbCommand cmd = new OleDbCommand(query, conn);
                        cmd.Connection.Open();
                        conn1.Open();
                        using (OleDbDataReader rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                status = 1;
                                ce = rd["group_ce"].ToString().Length == 0 ? 0 : 1;
                                cs = rd["group_cs"].ToString().Length == 0 ? 0 : 1;
                                it = rd["group_it"].ToString().Length == 0 ? 0 : 1;
                                if (rd["course_name"].ToString().Contains("*")) status = 0;
                                if (rd["Coordinator"].ToString().Length > 0)
                                {
                                    names = rd["Coordinator"].ToString().Split(' ');
                                }

                                cmd1 = new SqlCommand(query1, conn1);
                                cmd1.Parameters.Add(new SqlParameter("@year", SqlDbType.Int)).Value = current_year;
                                cmd1.Parameters.Add(new SqlParameter("@semester", SqlDbType.VarChar, 11)).Value = current_semester;
                                cmd1.Parameters.Add(new SqlParameter("@department", SqlDbType.VarChar, 11)).Value = current_department;
                                cmd1.Parameters.Add(new SqlParameter("@course_number", SqlDbType.VarChar, 11)).Value = rd["course_number"].ToString();
                                cmd1.Parameters.Add(new SqlParameter("@coordinator_name", SqlDbType.VarChar, 50)).Value = rd["Coordinator"].ToString();
                                cmd1.Parameters.Add(new SqlParameter("@coordinator_id", SqlDbType.VarChar, 50)).Value = rd["Coordinator_ID"].ToString();
                                cmd1.Parameters.Add(new SqlParameter("@display_name", SqlDbType.VarChar, 100)).Value = rd["course_name"].ToString();
                                cmd1.Parameters.Add(new SqlParameter("@group_ce", SqlDbType.TinyInt)).Value = ce;
                                cmd1.Parameters.Add(new SqlParameter("@group_cs", SqlDbType.TinyInt)).Value = cs;
                                cmd1.Parameters.Add(new SqlParameter("@group_it", SqlDbType.TinyInt)).Value = it;
                                cmd1.Parameters.Add(new SqlParameter("@status", SqlDbType.TinyInt)).Value = status;
                                cmd1.Parameters.Add(new SqlParameter("@first_name", SqlDbType.VarChar, 50)).Value = names[0];
                                cmd1.Parameters.Add(new SqlParameter("@last_name", SqlDbType.VarChar, 50)).Value = names[1];
                                cmd1.Parameters.Add(new SqlParameter("@euid", SqlDbType.VarChar, 50)).Value = rd["Coordinator_ID"].ToString();
                                cmd1.Parameters.Add(new SqlParameter("@role", SqlDbType.Int)).Value = 2;    // 2 for coordinator
                                cmd1.Parameters.Add(new SqlParameter("@faculty_type", SqlDbType.VarChar, 11)).Value = "Full-Time";  // full-time unless specified
                                result = Convert.ToInt32(cmd1.ExecuteScalar());

                                for (i = 1; i <= 9; i++)
                                {
                                    outcome = String.Concat("Outcome", i);
                                    if (rd[outcome].ToString().Length != 0)
                                    {
                                        cmd1 = new SqlCommand(query2, conn1);
                                        cmd1.Parameters.Add(new SqlParameter("@num", SqlDbType.Int)).Value = i;
                                        cmd1.Parameters.Add(new SqlParameter("@course_outcome", SqlDbType.VarChar, -1)).Value = rd[outcome].ToString();
                                        cmd1.Parameters.Add(new SqlParameter("@course_id", SqlDbType.Int)).Value = result;
                                        cmd1.ExecuteNonQuery();
                                    }
                                }
                                for (i = 0; i < 3; i++)
                                {
                                    for (j = 1; j <= 14; j++)
                                    {
                                        student_outcome = p[i] + "StudentOutcomes" + j;
                                        if (rd[student_outcome].ToString().Length > 0)
                                        {
                                            string word = rd[student_outcome].ToString();
                                            string[] words = word.Split(", ");
                                            if (!int.TryParse(words[0], out _)) break;
                                            for (k = 0; k < words.Length; k++)
                                            {
                                                t = Convert.ToInt32(words[k]);
                                                cmd1 = new SqlCommand(query3, conn1);
                                                cmd1.Parameters.Add(new SqlParameter("@course_id", SqlDbType.Int)).Value = result;
                                                cmd1.Parameters.Add(new SqlParameter("@program", SqlDbType.VarChar, 10)).Value = programs[i];
                                                cmd1.Parameters.Add(new SqlParameter("@student_outcome_order", SqlDbType.Int)).Value = j;
                                                cmd1.Parameters.Add(new SqlParameter("@course_outcome_order", SqlDbType.Int)).Value = t;
                                                cmd1.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        return sqlReturn;
                    }
                    catch (Exception ex)
                    {
                        sqlReturn.message = ex.Message;
                        sqlReturn.code = -1;
                        return sqlReturn;
                    }
                }
            }
        }

        public SqlReturn PostAttachmentInfo(string filePath, string originalFileName, int outcomeId)
        {
            SqlReturn sqlReturn = new SqlReturn();

            sqlReturn.code = 1;
            return sqlReturn;
        }

        public bool AddProgram(string Program)
        {
            return true;
        }
    }
}
