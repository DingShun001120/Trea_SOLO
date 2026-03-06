<%*

let url = 'https://www.tianqi.com/binhuqu/';

let weather = '无锡 天气获取失败 Weather fetch failed';

try {

  let res = await request({url: url, method: "GET"});

  res = res.replace(/\s/g,'');

  let r = /<ddclass="weather">[\s\S]*?<\/dd>/g;

  let data = r.exec(res)[0];

  r = /<span><b>(.*?)<\/b>(.*?)<\/span>/g;

  data = r.exec(data);

  weather = '无锡 ' + data[1] + ' ' + data[2];

} catch(e) {}

-%>

---

type: Work-Daily

tags:

  - review/daily

  - work/engineering

date: <% tp.file.creation_date("YYYY-MM-DD-dddd") %>

weather: <% weather %>

project:

sprint:

jira_board:

Habit_1:

Habit_2:

Habit_3:

Habit_4:

---


# 工程师工作日记 | Engineer Daily Log

  

> 周计划链接 | Weekly Plan Link

> 根据你的周报命名规则调整以下引用

![[Weekly <% tp.date.now("YYYY-ww", -7) %>#This week]]

  

## 概览 | Overview

- 今日主题 | Today's Theme:

- 工作时段 | Work Hours:

- 专注目标 | Focus Goals:

  

## 站会 | Standup

- 昨天 | Yesterday:

- 今天 | Today:

- 阻碍 | Blockers:

  

## 计划 | Planning

- Top 3:

  - [ ]

  - [ ]

  - [ ]

- 关键任务 | Key Tasks:

  - [ ]

- 外部依赖 | External Dependencies:

  

## 开发 | Development

- 分支 | Branch:

- 提交 | Commits:

- 变更摘要 | Change Summary:

- PR链接 | PR Links:

- 测试 | Tests:

  - 方案 | Plan:

  - 结果 | Results:

- 部署 | Deployment:

  - 环境 | Environment:

  - 状态 | Status:

  

## 缺陷 | Bugs

- 复现步骤 | Repro Steps:

- 初步分析 | Initial Analysis:

- 已尝试 | Attempts:

- 解决方案 | Fix Plan:

  

## 会议 | Meetings

- 议程 | Agenda:

- 结论 | Decisions:

- 待办 | Action Items:

  

## 学习 | Learning

- 今日学习 | Today's Learning:

- 资料链接 | Resources:

  

## 记录 | Notes

- 重要笔记 | Important Notes:

  

## 时间记录 | Time Tracking

- 任务 | Task:

- 开始 | Start:

- 结束 | End:

- 时长 | Duration:

  

## 日终总结 | End of Day Summary

- 成就 | Achievements:

- 教训 | Lessons:

- 明日计划 | Plan for Tomorrow:

  

## 日终清单 | End-of-Day Checklist

- [ ] GTD整理 | GTD inbox zero

- [ ] 账本 | Bookkeeping

- [ ] 备份 | Backup

  - [ ] Obsidian Vault

- [ ] 清理工作区 | Clean workspace

- [ ] 邮件收件箱 | Email inbox

  

## 今日创建 | Notes Created Today

```dataview

list

where file.cday = date(<% tp.file.creation_date("YYYY-MM-DD") %>)

sort file.ctime asc

```

  

## 今日修改 | Notes Modified Today

```dataview

list

where file.mday = date(<% tp.file.creation_date("YYYY-MM-DD") %>)

sort file.mtime asc

```

  

## 习惯追踪（可选） | Habit Tracker (Optional)

- Habit_1:

- Habit_2:

- Habit_3:

- Habit_4: