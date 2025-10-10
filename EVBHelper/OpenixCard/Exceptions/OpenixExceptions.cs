namespace OpenixCard.Exceptions;

internal class OpenixException(string message) : Exception(message);

internal sealed class FileOpenException(string path)
    : OpenixException($"无法打开文件: {path}");

internal sealed class FileFormatException(string path)
    : OpenixException($"文件不是有效的 Allwinner 镜像: {path}");

internal sealed class FileSizeException(string path)
    : OpenixException($"文件大小无效: {path}");

internal sealed class NoInputProvidedException()
    : OpenixException("未提供输入文件或目录。");

internal sealed class MissingOperatorException()
    : OpenixException("未指定操作参数，请至少提供 -u/-d/-p/-s 中的一项。");

internal sealed class OperatorError(string reason)
    : OpenixException($"操作失败: {reason}");
