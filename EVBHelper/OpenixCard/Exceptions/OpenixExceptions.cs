namespace OpenixCard.Exceptions;

internal class OpenixException(string message) : Exception(message);

internal sealed class FileOpenException(string path)
    : OpenixException($"�޷����ļ�: {path}");

internal sealed class FileFormatException(string path)
    : OpenixException($"�ļ�������Ч�� Allwinner ����: {path}");

internal sealed class FileSizeException(string path)
    : OpenixException($"�ļ���С��Ч: {path}");

internal sealed class NoInputProvidedException()
    : OpenixException("δ�ṩ�����ļ���Ŀ¼��");

internal sealed class MissingOperatorException()
    : OpenixException("δָ�������������������ṩ -u/-d/-p/-s �е�һ�");

internal sealed class OperatorError(string reason)
    : OpenixException($"����ʧ��: {reason}");
