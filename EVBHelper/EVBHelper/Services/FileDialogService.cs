using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EVBHelper.Services
{
    public interface IFileDialogService
    {
        Task<string?> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default);

        Task<string?> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class FileDialogRequest
    {
        public string? Title { get; init; }

        public IReadOnlyList<FilePickerFileType>? Filters { get; init; }

        public bool AllowMultiple { get; init; }

        public string? SuggestedFileName { get; init; }

        public string? DefaultExtension { get; init; }
    }

    public sealed class DesktopFileDialogService : IFileDialogService
    {
        private readonly Window _owner;

        public DesktopFileDialogService(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task<string?> OpenFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default)
        {
            if (!_owner.StorageProvider.CanOpen)
            {
                return null;
            }

            var options = new FilePickerOpenOptions
            {
                Title = request.Title,
                AllowMultiple = request.AllowMultiple
            };

            if (request.Filters is { Count: > 0 })
            {
                options.FileTypeFilter = request.Filters.ToList();
            }

            var result = await _owner.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
            if (result is null || result.Count == 0)
            {
                return null;
            }

            return request.AllowMultiple ? string.Join(";", result.Select(static file => file.Path.LocalPath)) : result[0].Path.LocalPath;
        }

        public async Task<string?> SaveFileAsync(FileDialogRequest request, CancellationToken cancellationToken = default)
        {
            if (!_owner.StorageProvider.CanSave)
            {
                return null;
            }

            var options = new FilePickerSaveOptions
            {
                Title = request.Title,
                SuggestedFileName = request.SuggestedFileName,
                ShowOverwritePrompt = true
            };

            if (!string.IsNullOrWhiteSpace(request.DefaultExtension))
            {
                options.DefaultExtension = request.DefaultExtension;
            }

            if (request.Filters is { Count: > 0 })
            {
                options.FileTypeChoices = request.Filters.ToList();
            }

            var result = await _owner.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(true);
            return result?.Path.LocalPath;
        }
    }
}
