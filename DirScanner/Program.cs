using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DirScanner;

/// <summary>
/// Головний клас програми для сканування директорій та експорту назв файлів.
/// </summary>
class Program
{
    #region Entry Point

    /// <summary>
    /// Головний метод програми. Керує життєвим циклом виконання.
    /// </summary>
    /// <param name="args">Аргументи командного рядка.</param>
    [STAThread]
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Обираємо директорію
        string? targetDirectory = SelectDirectory();
        if (string.IsNullOrEmpty(targetDirectory)) return;

        Console.WriteLine($"\n[INFO] Обрано шлях: {targetDirectory}");

        try
        {
            // Отримуємо список унікальних розширень
            var extensions = GetUniqueExtensions(targetDirectory);
            if (!extensions.Any())
            {
                Console.WriteLine("[WARN] У цій папці не знайдено файлів з розширеннями.");
                return;
            }

            // Запитуємо у користувача, яке розширення його цікавить
            string? selectedExt = PromptUserForExtension(extensions);
            if (selectedExt == null) return;

            // Отримуємо список імен файлів за обраним розширенням
            var filteredFiles = GetFileNamesByExtension(targetDirectory, selectedExt);

            // Зберігаємо звіт у файл
            SaveResultsToFile(targetDirectory, selectedExt, filteredFiles);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Сталася критична помилка: {ex.Message}");
        }

        Console.WriteLine("\nРоботу завершено. Натисніть будь-яку клавішу для виходу...");
        Console.ReadKey();
    }

    #endregion

    #region Business Logic

    /// <summary>
    /// Шукає всі файли в папці та виокремлює список унікальних розширень.
    /// </summary>
    /// <param name="path">Шлях до папки.</param>
    /// <returns>Список розширень у нижньому регістрі, відсортований за алфавітом.</returns>
    private static List<string> GetUniqueExtensions(string path)
    {
        return Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetExtension(f).ToLower())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .OrderBy(e => e)
            .ToList();
    }

    /// <summary>
    /// Фільтрує файли в директорії за конкретним розширенням.
    /// </summary>
    /// <param name="path">Шлях до папки.</param>
    /// <param name="ext">Обране розширення (наприклад, ".txt").</param>
    /// <returns>Список назв файлів без повних шляхів.</returns>
    private static List<string> GetFileNamesByExtension(string path, string ext)
    {
        return Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList()!;
    }

    /// <summary>
    /// Формує вміст звіту та записує його у файл за обраним шляхом.
    /// </summary>
    /// <param name="sourcePath">Яка папка сканувалася.</param>
    /// <param name="extension">Яке розширення було обрано.</param>
    /// <param name="files">Список знайдених імен файлів.</param>
    private static void SaveResultsToFile(string sourcePath, string extension, List<string> files)
    {
        string defaultName = $"list_of_{extension.TrimStart('.')}.txt";
        string? savePath = GetSavePath(defaultName);

        if (string.IsNullOrEmpty(savePath))
        {
            Console.WriteLine("[INFO] Збереження скасовано користувачем.");
            return;
        }

        // Формуємо "шапку" файлу та список
        var content = new List<string>
        {
            $"Звіт згенеровано: {DateTime.Now}",
            $"Сканована директорія: {sourcePath}",
            $"Обране розширення: {extension}",
            new string('-', 60),
            ""
        };
        content.AddRange(files);

        File.WriteAllLines(savePath, content);
        Console.WriteLine($"\n--- УСПІХ! ---");
        Console.WriteLine($"Знайдено: {files.Count} файлів.");
        Console.WriteLine($"Звіт збережено: {savePath}");
    }

    #endregion

    #region User Interaction

    /// <summary>
    /// Виводить список знайдених розширень у консоль та отримує вибір користувача.
    /// </summary>
    /// <param name="extensions">Список доступних розширень.</param>
    /// <returns>Обране розширення або null, якщо вибір некоректний.</returns>
    private static string? PromptUserForExtension(List<string> extensions)
    {
        Console.WriteLine("\nЗнайдені типи файлів у цій директорії:");
        for (int i = 0; i < extensions.Count; i++)
            Console.WriteLine($"  {i + 1}. {extensions[i]}");

        Console.Write("\nВведіть номер розширення для експорту: ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= extensions.Count)
            return extensions[choice - 1];

        Console.WriteLine("[ERROR] Невірний номер. Спробуйте ще раз.");
        return null;
    }

    /// <summary>
    /// Відкриває системний діалог вибору папки залежно від ОС.
    /// </summary>
    /// <returns>Повний шлях до папки або null.</returns>
    private static string? SelectDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var fbd = new FolderBrowserDialog { Description = "Оберіть папку для аналізу" };
            return fbd.ShowDialog() == DialogResult.OK ? fbd.SelectedPath : null;
        }
        
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? RunCommand("osascript", "-e \"tell application \\\"System Events\\\" to POSIX path of (choose folder)\"")
            : RunCommand("zenity", "--file-selection --directory");
    }

    /// <summary>
    /// Відкриває системний діалог збереження файлу залежно від ОС.
    /// </summary>
    /// <param name="defaultName">Назва файлу за замовчуванням.</param>
    /// <returns>Повний шлях для збереження або null.</returns>
    private static string? GetSavePath(string defaultName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var sfd = new SaveFileDialog 
            { 
                Filter = "Text Files (*.txt)|*.txt", 
                FileName = defaultName,
                Title = "Зберегти список файлів як..."
            };
            return sfd.ShowDialog() == DialogResult.OK ? sfd.FileName : null;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? RunCommand("osascript", $"-e \"tell application \\\"System Events\\\" to POSIX path of (choose file name default name \\\"{defaultName}\\\")\"")
            : RunCommand("zenity", $"--file-selection --save --confirm-overwrite --filename=\"{defaultName}\"");
    }

    #endregion

    #region OS Helpers

    /// <summary>
    /// Запускає зовнішню системну команду та зчитує її стандартний вивід.
    /// Використовується для виклику AppleScript або Zenity.
    /// </summary>
    /// <param name="exe">Виконавчий файл (osascript, zenity).</param>
    /// <param name="args">Аргументи команди.</param>
    /// <returns>Результат виконання команди у вигляді рядка.</returns>
    private static string? RunCommand(string exe, string args)
    {
        try
        {
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = exe, 
                    Arguments = args, 
                    RedirectStandardOutput = true,
                    UseShellExecute = false, 
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch { return null; }
    }

    #endregion
}