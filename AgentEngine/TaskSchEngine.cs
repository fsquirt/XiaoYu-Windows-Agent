using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XiaoYu_LAM.AgentEngine
{
    internal class TaskSchEngine
    {
        // 注册计划任务文件夹
        public static void CreateTaskSchFolder(string folderPath = @"\XiaoYu_Agnet")
        {
            // 连接本地计划任务服务，using自动释放资源
            using (var taskService = new TaskService())
            {
                try
                {
                    taskService.RootFolder.CreateFolder(folderPath);
                    Console.WriteLine($"计划任务文件夹 {folderPath} 创建成功");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("权限不足，请以管理员身份运行程序");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建文件夹失败：{ex.Message}");
                }
            }
        }

        // 删除计划任务文件夹
        public static void DeleteTaskSchFolder(string folderPath = @"\XiaoYu_Agnet")
        {
            using (var taskService = new TaskService())
            {
                try
                {
                    // 第二个参数true：递归删除文件夹内的所有任务和子文件夹；如果传false，文件夹非空时会报错
                    taskService.RootFolder.DeleteFolder(folderPath.TrimStart('\\'), true);
                    Console.WriteLine($"文件夹 {folderPath} 已删除");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("文件夹不存在");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"删除文件夹失败：{ex.Message}");
                }
            }
        }

        // 列出根目录计划任务文件夹
        public static List<String> ListTaskSchFolders()
        {
            List<String> TaskFolder = new List<string> { };
            using (var taskService = new TaskService())
            {
                Console.WriteLine("计划任务文件夹列表：");
                foreach (var folder in taskService.RootFolder.SubFolders)
                {
                    Console.WriteLine(folder.Name);
                    TaskFolder.Append(folder.Name);
                }
            }

            return TaskFolder;
        }

        // 列出指定文件夹中的计划任务
        public static void ListTaskFolderTasks(string folderPath = @"\XiaoYu_Agnet")
        {
            using (var taskService = new TaskService())
            {
                try
                {
                    var folder = taskService.GetFolder(folderPath);
                    Console.WriteLine($"文件夹 {folderPath} 中的计划任务：");
                    foreach (var task in folder.Tasks)
                    {
                        Console.WriteLine(task.Name);
                        Console.WriteLine($"  触发器：{string.Join(", ", task.Definition.Triggers.Select(t => t.TriggerType))}");
                        Console.WriteLine($"  操作内容：{task.Definition.Actions.ToString()}");
                        Console.WriteLine($"  状态：{task.State}");
                    }
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("文件夹不存在");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"列出任务失败：{ex.Message}");
                }
            }
        }

        // 创建计划任务，默认放在\XiaoYu_Agnet文件夹下
        public static void CreateTask(string taskName, string arguments , string Frequency , int hour , int minute , string folderPath = @"\XiaoYu_Agnet")
        {
            using (var taskService = new TaskService())
            {
                arguments = "--task " + arguments; 
                try
                {
                    var folder = taskService.GetFolder(folderPath);
                    var taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = $"自动执行LLM任务：{taskName}";

                    if(arguments == "Once") //只运行一次
                    {
                        taskDefinition.Triggers.Add(new TimeTrigger { StartBoundary = DateTime.Now.AddHours(hour).AddMinutes(minute) }); // X小时Y分钟后执行一次
                    }
                    else if (Frequency == "Daily")
                    {
                        taskDefinition.Triggers.Add(new DailyTrigger { StartBoundary = DateTime.Now.AddHours(hour).AddMinutes(minute) }); // 每天X小时Y分钟执行
                    }
                    else
                    {
                        return;
                    }

                    taskDefinition.Actions.Add(new ExecAction(Application.ExecutablePath, arguments, Environment.CurrentDirectory));
                    folder.RegisterTaskDefinition(taskName, taskDefinition);
                    Console.WriteLine($"计划任务 {taskName} 创建成功");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("文件夹不存在");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("权限不足，请以管理员身份运行程序");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建计划任务失败：{ex.Message}");
                }
            }
        }
    }
}
