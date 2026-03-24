// 临时脚本：生成 123456 的 BCrypt 哈希
// 使用方式: dotnet script reset_password.csx（需要 dotnet-script 工具）
// 或者直接在项目中临时运行

#r "nuget: BCrypt.Net-Next, 4.0.3"

using BCrypt.Net;

var password = "123456";
var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
Console.WriteLine($"BCrypt hash for '{password}':");
Console.WriteLine(hash);
