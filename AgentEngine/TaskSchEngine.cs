using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XiaoYu_LAM.AgentEngine
{
    internal class TaskSchEngine
    {
        public static string folderPath = @"\XiaoYu_Agnet";

        // 注册计划任务文件夹
        public static void CreateTaskSchFolder()
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
                catch (System.Runtime.InteropServices.COMException)
                {
                    //Console.WriteLine("文件夹已存在 无需再注册");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建文件夹失败：{ex.Message}");
                }
            }
        }

        // 删除计划任务文件夹
        public static void DeleteTaskSchFolder()
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

        // 返回指定文件夹中的计划任务信息集合（任务名称 触发器 任务描述 任务内容 下次执行时间）
        public static List<TaskInfo> ListTaskFolderTasks()
        {
            var results = new List<TaskInfo>();

            using (var taskService = new TaskService())
            {
                try
                {
                    var folder = taskService.GetFolder(folderPath);

                    foreach (var task in folder.Tasks)
                    {
                        // 构建操作内容的可读描述（尽量提取 ExecAction 的 Path 和 Arguments）
                        string actionsDescription = string.Join("; ", task.Definition.Actions.Select(a =>
                        {
                            try
                            {
                                var exec = a as ExecAction;
                                if (exec != null)
                                {
                                    return $"Exec: Path={exec.Path}, Arguments={exec.Arguments}";
                                }

                                return a.ToString();
                            }
                            catch
                            {
                                return a.GetType().Name;
                            }
                        }));

                        // 构建触发器描述
                        string triggersDescription = string.Join("; ", task.Definition.Triggers.Select(t =>
                        {
                            try
                            {
                                var start = "";
                                try { start = t.StartBoundary != DateTime.MinValue ? $", Start={t.StartBoundary}" : ""; } catch { }
                                return $"{t.TriggerType}{start}";
                            }
                            catch
                            {
                                return t.GetType().Name;
                            }
                        }));

                        string description = null;
                        try { description = task.Definition?.RegistrationInfo?.Description; } catch { description = null; }

                        DateTime? nextRun = null;
                        try { nextRun = task.NextRunTime; } catch { nextRun = null; }

                        results.Add(new TaskInfo
                        {
                            Name = task.Name,
                            Triggers = triggersDescription,
                            Description = description,
                            Actions = actionsDescription,
                            NextRunTime = nextRun
                        });
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

            return results;
        }

        // 删除计划任务
        public static void DeleteTask(string taskName)
        {
            using (var taskService = new TaskService())
            {
                try
                {
                    var folder = taskService.GetFolder(folderPath);
                    folder.DeleteTask(taskName);
                    Console.WriteLine($"计划任务 {taskName} 已删除");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("任务不存在");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"删除计划任务失败：{ex.Message}");
                }
            }
        }

        // 创建计划任务，默认放在\XiaoYu_Agnet文件夹下
        [Description("创建计划任务，会在指定时间自动开始执行任务，taskName参数为任务名称，arguments参数为任务内容，Frequency是任务频率(只能传入'Once'或者'Daily')，hour和minute分别为小时和分钟")]
        public static string CreateTask(string taskName, string arguments, string Frequency, int hour, int minute)
        {
            using (var taskService = new TaskService())
            {
                arguments = "--task " + arguments;
                try
                {
                    var folder = taskService.GetFolder(folderPath);
                    var taskDefinition = taskService.NewTask();
                    taskDefinition.RegistrationInfo.Description = $"自动执行LLM任务：{taskName}";

                    if (Frequency == "Once") //只运行一次
                    {
                        taskDefinition.Triggers.Add(new TimeTrigger { StartBoundary = DateTime.Now.AddHours(hour).AddMinutes(minute) }); // X小时Y分钟后执行一次
                    }
                    else if (Frequency == "Daily")
                    {
                        taskDefinition.Triggers.Add(new DailyTrigger { StartBoundary = DateTime.Now.AddHours(hour).AddMinutes(minute) }); // 每天X小时Y分钟执行
                    }
                    else
                    {
                        return "传入了未知Frequency参数 只能传入Once或者Daily Once代表只执行一次 Daily代表每天执行";
                    }

                    taskDefinition.Actions.Add(new ExecAction(Application.ExecutablePath, arguments, Environment.CurrentDirectory));
                    folder.RegisterTaskDefinition(taskName, taskDefinition);
                    Console.WriteLine($"计划任务 {taskName} 创建成功");
                    return "计划任务创建成功";
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("文件夹不存在");
                    return "文件夹不存在";
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("权限不足，请以管理员身份运行程序");
                    return "权限不足";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建计划任务失败：{ex.Message}");
                    return $"创建计划任务失败：{ex.Message}";
                }
            }
        }
    }

    // 计划任务信息 DTO
    internal class TaskInfo
    {
        public string Name { get; set; }
        public string Triggers { get; set; }
        public string Description { get; set; }
        public string Actions { get; set; }
        public DateTime? NextRunTime { get; set; }
    }

}
