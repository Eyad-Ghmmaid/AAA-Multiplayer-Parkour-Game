using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FS_Core
{
    public interface ISavable
    {
        object CaptureState();
        void RestoreState(object state);

        Type GetSavaDataType();
    }
}
