using System;
using EVBHelper.Services;
using EVBHelper.ViewModels.Dtb;
using EVBHelper.ViewModels.Gpt;
using EVBHelper.ViewModels.Rfel;

namespace EVBHelper.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(IRfelCliService rfelCliService, IFileDialogService fileDialogService)
    {
        if (rfelCliService is null)
        {
            throw new ArgumentNullException(nameof(rfelCliService));
        }

        if (fileDialogService is null)
        {
            throw new ArgumentNullException(nameof(fileDialogService));
        }

        Rfel = new RfelViewModel(rfelCliService, fileDialogService);
        DtbEditor = new DtbEditorViewModel(fileDialogService);
        GptEditor = new GptEditorViewModel(fileDialogService);
    }

    public RfelViewModel Rfel { get; }

    public DtbEditorViewModel DtbEditor { get; }

    public GptEditorViewModel GptEditor { get; }
}
