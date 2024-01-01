# 中文名

提供了简单的方法来扩展原版的命名方式, 使得命名更加灵活.

不要再直接修改该mod的代码来添加命名/词库了, 使用扩展方式.

## 1. 概览

这个版本的中文名提供了非常灵活的命名方式. 主要由四个部分组成: 命名器, 词库, 参数获取器, 模板选择器(未实现,
不远的未来会实现).

### 1.1 模组如何工作

1. 接收命名器, 添加/替换原版同id的命名器为一个固定的命名器 `A`(用于识别该命名器是否被替换)
2. 等待命名事件, 当目标名字为 `A`的结果时, 继续, 否则跳过
3. 以请求的 `name_template_id`来查找该模组中的命名器 `B`, 如果找到, 则使用该命名器 `B`, 否则跳过
4. 根据 `B`的参数获取器获取参数 `P`, 并使用 `B`的模板选择器选择模板 `T`(目前仅有按权重随机获取)
5. 使用 `T`和 `P`来生成名字, 如果生成失败(比如模板中某个部分生成空字符串), 则重新随机选择模板 `T`并重试, 直到成功或到达最大重试次数
6. 如果到达最大重试次数, 则使用 `B`的默认模板 $T_d $来生成名字

## 2. 词库

每一个词库都是一个纯文本文件, 一行表示一个词, 不提供注释等功能.

词库文件的无后缀名部分即为词库的id, 词库id不可重复(区分大小写, 要保证与其他模组也不重复, 如果重复,
则后提交的词库会覆盖之前提交的词库).

## 2.1 提交词库文件

### 2.1.1 一般提交方法

在初始化的地方添加

```csharp
WordLibraryManager.SubmitDirectoryToLoad("path/to/your/word/library/directory");
```

即可.

如果你的词库文件夹的路径相对于自己模组的根目录为"additional_resources/word_libraries",
并且你的模组主类实现了`IMod`接口或继承了`BasicMod<T>`, 则可以使用

```csharp
WordLibraryManager.SubmitDirectoryToLoad(Path.Combine(ModClass.Instance.GetDeclaration().FolderPath, "additional_resources/word_libraries"));
```

其中`ModClass`替换成你的模组主类的类名.

注意!!! 添加`using Chinese_Name;`.

注意!!! 如果你的模组直接依赖于中文名, 则可以直接使用上面代码; 否则, 需要使用块

```csharp
#if 一米_中文名
//代码...
#endif
```

来包裹`using Chinese_Name;`和上述代码.

### 2.1.2 单个提交(覆盖)

当你使用这个功能时, 你应该知道你在做什么.

你可以使用函数`WordLibraryManager.Submit(string pId, List<string> pWords)`.

注意点见[2.1.1](#211-一般提交方法)

### 2.1.3 单个提交(追加)

当你使用这个功能时, 你应该知道你在做什么.

你可以使用函数`WordLibraryManager.SubmitForPatch(string pId, List<string> pWords)`.

这个函数在提交时会检查是否已经存在该id的词库, 如果存在, 则会将新的词库追加到原词库的末尾.

注意点见[2.1.1](#211-一般提交方法)

### 2.1.4 已有的词库

|  词库id  |   说明   |
|:------:|:------:|
|  千字文   |   -    |
|  百家姓   |   -    |
|  常用格言  |   -    |
|  真实国名  | 中国古代国名 |
|  真实城名  | 中国古代城名 |
|  西方名字  |   -    |
|  西方姓氏  |   -    |
| 西方国名后缀 |   -    |
| 西方名字中缀 |   -    |

## 3. 命名器

每个用来描述命名器的文件都是一个JSON文件, 单个文件可以描述多个命名器, 文件名仅用作标识.

### 3.1 文件格式

每个文件会被解析成一个`List<CN_NameGeneratorAsset>`实例, 具体的

```csharp
public class CN_NameGeneratorAsset{
    public string id;
    public List<CN_NameTemplate> templates;
    public string parameter_getter = "default"; // 局部参数获取器的id
    public CN_NameTemplate default_template = CN_NameTemplate.Create("#NO_NAME#", 1); // 默认的命名模板
}

public class CN_NameTemplate{
    public string format; // 模板的格式
    public float weight = 1; // 权重
}
```

其中无默认值的字段为必填字段.

`CN_NameGeneratorAsset`的id不可重复, 且应当为你要使用的`name_template_id`. 比如你要为冥族(修仙模组中的一个种族)的城市提供命名器,
那么`CN_NameGeneratorAsset`的id则应当是冥族的`Race::name_template_city`的值.

### 3.2 命名模板

在命名模板中有几种关键字符, 用于解析模板:

1. "{"和"}", 它们用于构造一般词库原子
2. "<"和">", 它们用于构造必填词库原子
3. "$", 用于构造引用(参数或者标签)原子
4. "#", 用于构造纯文本原子
5. ":", 仅用于在词库原子中划分"标签"和"格式"

未来会添加转义字符来解决这几种字符占用的问题

所有原子都存在一个值, 这个值永远是字符串类型的, 用于拼接得到结果.

原子的"内容"即由它们的关键字符所包裹的字符串.

下面介绍各个原子的求值方式

#### 3.2.1 纯文本原子

纯文本原子的值即为其内容

#### 3.2.2 引用原子

引用原子的求值有两种情况:

1. 取参, 以其内容为id尝试从[全局参数]和[传入参数]中获取值
2. 引用标签, 该原子的取值会晚于其引用标签的原子, 值为其引用标签的原子的值

当该原子的值为空时, 将会直接判定其所在模板生成失败

注意, 避免循环引用

#### 3.2.3 一般词库原子

其内容被":"左右分割为"格式"和"标签"两个部分. 格式用来确定将会使用哪一个词库, 标签用于存储该原子的值(可以用于引用原子,
同时也可以用于自定义命名获取各个部分的值).

当该原子的值为空时, 将会直接判定其所在模板生成失败

##### 3.2.3.1 标签

标签应当是纯文本, 不应包含前面任一关键字符

##### 3.2.3.2 格式

格式与标签类似, 但格式中可以嵌套一层的引用原子(可以多个, 但只能嵌套一层).

下面是一个示例

"{通用月份词库之$month$:example_tag}"

这个会在游戏一月时访问词库"通用月份词库之january"

#### 3.2.4 必填词库原子

与一般词库原子基本一致

当该原子的值为空或其格式所需的参数/标签不存在(允许存在但为空的情况)时, 将会直接判定其所在模板生成失败.

### 3.3 提交命名器

### 2.1.1 一般提交方法

在初始化的地方添加

```csharp
CN_NameGeneratorLibrary.SubmitDirectoryToLoad("path/to/your/name/generators/directory");
```

即可.

如果你的词库文件夹的路径相对于自己模组的根目录为"additional_resources/name_generators",
并且你的模组主类实现了`IMod`接口或继承了`BasicMod<T>`, 则可以使用

```csharp
WordLibraryManager.SubmitDirectoryToLoad(Path.Combine(ModClass.Instance.GetDeclaration().FolderPath, "additional_resources/name_generators"));
```

其中`ModClass`替换成你的模组主类的类名.

### 2.1.2 单个提交(覆盖)

当你使用这个功能时, 你应该知道你在做什么.

你需要手动创建一个`CN_NameGeneratorAsset`实例,

设置其`parameter_getter`为你要使用的局部参数获取器的id,

设置其`default_template`为你要使用的默认模板,

设置其`templates`为你要使用的模板列表,

设置其`id`为你要使用的`name_template_id`.

你可以用`CN_NameTemplate.Create(string pFormat, float pWeight)`来创建模板.

然后你可以使用函数`CN_NameGeneratorLibrary.Submit(CN_NameGeneratorAsset pAsset)`来提交.

注意点见[2.1.1](#211-一般提交方法)

## 4. 参数获取器

在3.2.3.2中出现了"$month$"参数, 这是中文名提供的全局参数.

参数获取器总体分为两类: 全局参数获取器, 局部参数获取器.

如果要表示参数确实, 需要保证参数的key不存在于参数表中, 而不是将其值设为空字符串.

### 4.1 全局参数获取器

全局参数获取器生成的参数可以在所有的命名器中使用, 即使它指定使用了其他的局部参数获取器.

全局参数由一个`MonoBehaviour`实例的`Update`进行实时维护, 并储存在一个字典中.

具体的, 你需要提供一个类型为`Action<Dictionary<string, string>>`委托, 其参数即为存储全局参数的字典.

#### 4.1.1 示例

```csharp
private static void example_global_parameter_getter(Dictionary<string, string> pParameters)
{
    pParameters["world_type"] = GetWorldType();
}
```

其中`GetWorldType`返回了这个世界的类型, 这只是一个示例, 原版游戏并没有世界类型相关的定义.

#### 4.1.2 提交

在初始化的地方添加

```csharp
ParameterGetters.PutGlobalParameterGetter(example_global_parameter_getter);
```

即可.

注意点见[2.1.1](#211-一般提交方法)

#### 4.1.3 已有的全局参数

|  参数名   |  说明  |                    可能值                    |
|:------:|:----:|:-----------------------------------------:|
|  year  |  年份  |                    1~N                    |
| month  |  月份  | january~december <br>月份asset的english_name |
|  era   |  纪年  |                纪年asset的id                 |
| 天干地支纪年 | 天干地支 |                   甲子~癸亥                   |

### 4.2 局部参数获取器

局部参数获取器仅在命名器指定其参数获取器时被使用. 与全局参数类型获取器类似, 参数填写于传入的字典参数.

不同的是, 局部参数获取器具有各自的id, 并且有不同的类型, 相同类型的id不可重复, 不同类型的id可以重复.

#### 4.2.1 生物

##### 4.2.1.1 参数获取器委托类型

`Action<Actor, Dictionary<string, string>>`

##### 4.2.1.2 参数获取器委托参数说明

| 参数名         | 参数类型                       | 说明        | 备注      |
|-------------|----------------------------|-----------|---------|
| pActor      | Actor                      | 需要命名的生物   |         |
| pParameters | Dictionary<string, string> | 用于存储参数的字典 | 参数名不可重复 |

##### 4.2.1.3 示例

```csharp
private static void example_actor_parameter_getter(Actor pActor, Dictionary<string, string> pParameters){
    if (pActor.kingdom != null && pActor.kingdom.isCiv())
    {
        pParameters["kingdom"] = pActor.kingdom.data.name;
    }
}
```

当生物的国家不为空且为文明时, 将会将其国家的名字存储在参数"kingdom"中.

##### 4.2.1.4 提交

与[4.1.2](#412-提交)基本一致,
但是使用的是`ParameterGetters.PutActorParameterGetter(string, Action<Actor, Dictionary<string, string>>)`,
其中第一个参数为局部参数获取器的id.

注意点见[2.1.1](#211-一般提交方法)

##### 4.2.1.5 默认参数获取器

默认参数获取器的id为"default", 用于当命名器未指定参数获取器时使用.

|     参数名     |          说明           | 可能值 |
|:-----------:|:---------------------:|:---:|
| family_name | 姓氏, 会自动从生物的data中读出/写入 |  -  |

#### 4.2.2 城市

#### 4.2.3 国家

#### 4.2.4 文化

#### 4.2.5 氏族

#### 4.2.6 联盟

#### 4.2.7 战争

#### 4.2.8 装备

#### 4.2.9 自定义