# 词库及命名模板相关

检查worldbox_Data\StreamingAssets\mods\ChineseName目录下两个文件即可

参照已有内容仿写

对于词库，词库的id即为文件名(不包含后缀)，单行单项

对于命名模板，文件区分无实际含义，按照JSON格式编写Dictionary<string, NameGenerator>的实例，key为对应的原版中的name_template

class NameGenerator{

	public List<NameGeneratorTemplate> templates;
	
	public string generate_func_id;
	
}
class NameGeneratorTemplate{

	public float weight;
	
	public float template;
	
}

解释：

在需要通过name_template生成名字时，

从Dictionary<string, NameGenerator>获取对应的NameGenerator，

根据NameGeneratorTemplate.weight进行随机/可行性检查地选择NameGeneratorTemplate，

根据NameGeneratorTemplate.template生成草案，

由NameGenerator.generate_func_id获取得到的生成函数将草案细化为最终“名字”。


其中weight是权重, [0, float.MaxValue)

template按照如下格式组织

"{word_library_1_id, possibility_1}{word_library_2_id, possibility_2}{raw_word, possibility_3}{word_to_replace, possibility_4}"

其中

花括号表示一项；

第二项，possibility_*表示对应项的概率；

第一项，word_library_*_id表示对应项采用的词库的id，也可以填写单独的词，也可以填写要被替换的占位词。


注意：

替换用占位词仅在对应的generate_func中有效；

该Mod未进行任何的安全性处理，格式错误导致的异常该Mod不进行捕获处理。


实例参考已有文件。