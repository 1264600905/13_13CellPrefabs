写了5天的新功能，新鲜出炉。现在使用预制房间Mod可以三步导出一个带有房间布局的Mod，并且可以上传到创意工坊。


在启用了13✖13 Cell Prefabs 预制房间之后在开发者工具里会多出一些选项，

1.导出建筑  

2.合并建筑文件

3.导出为Mod


建议点上后面的锁定到快捷面板，可以这样直接放在左上角。


三个命令有先后次序，我会一个一个讲解作用。

1.导出建筑，点击后能看到这样的画面 

看起来很复杂？我一个个解释。     

 DefName是你建筑保存到xml（你就想像xml是个有规则的记事本就行），它是键值，是程序需要找到它的key值，你只需要知道，它这里只能输入字母，下划线_  和数字。  如果你英语不错或者想要后期找到它来修改，那么久输入这样的文本“4_Room”,"one_big_labor","Store_Room_Type2"。   

  也许你会说，这还是太麻烦了，有没有简单一点的办法，有的，有的，点随机生成就行，缺点是你可能很难找回这个建筑。

标签，描述和作者 分别对应最终展示建筑的三处显示，标签是1，描述是2，作者是3，如果希望支持多语言，我建议这里全部写英语，泰南神奇的翻译功能不会翻译某些英语字段，也许它们认为不需要把其他语言翻译成英语。


3.优先级  影响建筑的显示顺序，越小越靠前，可以是小数

4.类别    会影响最终放进哪个分类的建筑里，当前一共有12个分类，建议自己选一个已经存在的分类，如果你知道自己在干什么，可以自己去创造一共分类然后写这里，具体怎么操作，得看Alpha prefabs作者的github里面的指南wiki


不懂英语可以拿去翻译"AP_13_13_Dorm", "AP_13_13_Labor", "AP_13_13_Power", "AP_13_13_WorkShop", "AP_13_13_StoreRoom", "AP_13_13_Farm", "AP_13_13_Culture", "AP_13_13_Anomal", "AP_13_13_Defens", "AP_13_13_Atomics", "AP_13_13_Bath", "AP_13_13_Oil"     

选择确定之后，你需要选择两次，注意文本是，第一次选择左上，第二次选择右下。

第一次虽然有框框，但是那个框暂时不起作用，第一次框选其实是在指定需要截图的起点坐标，第二次框选才是选择范围。

（实现游戏内框选区域截图这个逻辑太太太蛋疼了，我至少试了十几版代码，暂时先这样，反正你可以多试试，又不会掉肉，我暂时不想动这个逻辑了，脑细胞Deading）



出现这种提示表示建筑导出完成了


导出文件在游戏根目录下面MyBuilding,  如果是随机生成名字，文件夹就是AP_20250521_140150这种，如果是自己写的DefName 名字就是这样AP_4_Room

2.合并建筑文件：
由于玩家可能一次性导出很多建筑，这个按钮可以根据建筑的类别，给他进行分类合并XML文件。比如导出三个建筑有12个xml文件，通过这个合并操作之后只有4个了。

你也不想加载个一年半载才进游戏吧？  合并后的建筑文件会输出到Merge文件夹里。


 导出为Mod：

点击之后可以直接把merge里的建筑组合成一个mod


模组名称就是模组名称拉，很简单的，导出的新Mod想叫啥就叫啥。

包名这个东西，绝对不能和其他Mod冲突，我建议写一个不容易重名的。只能输入字母数字和小点. 后面加上的后缀“cellprefabs.buildings”我不建议修改。

比如我这样写



Mod导出来了，看看样子。


游戏里瞅瞅？有的有的


放出来看看结构完整不？没有问题


怎么上传到创意工坊呢？


不过先打住啊！Mod是自动导出的，还有很多配置不够完善，下一期出一个如何修改完善Mod信息，然后上传创意工坊吧。

求点赞，求收藏！你的支持就是我的动力！


MIT License

Copyright (c) 2025 白金trigger

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
