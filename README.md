# 中文名

提供了简单的方法来扩展原版的命名方式, 使得命名更加灵活.

不要再直接修改该mod的代码来添加命名/词库了, 使用扩展方式.

## 使用方法

暂略, 简单来说就是创建一个mod, 然后创建两个文件夹, 分别放命名器和词库,
然后分别用`CN_NameGeneratorLibrary.SubmitDirectoryToLoad(string pDirectoryPath)`
和`WordLibraryManager.SubmitDirectoryToLoad(string pDirectoryPath)`加载.

### 命名器写法

暂略, 参考[默认生物命名](name_generators/default/creatures.json)

### 词库写法

单个文件作为一个词库, 词库无后缀的文件名即词库名, 词库文件一行一项