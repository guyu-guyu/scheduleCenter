using System.Collections.ObjectModel;

namespace ScheduleCenter.Gui
{
    public sealed class FolderNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }   // "" 表示根，"MyApp" 表示子文件夹
        public ObservableCollection<FolderNode> Children { get; private set; }

        public FolderNode()
        {
            Children = new ObservableCollection<FolderNode>();
        }
    }
}
