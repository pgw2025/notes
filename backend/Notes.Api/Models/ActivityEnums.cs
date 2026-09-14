namespace Notes.Api.Models;

/// <summary>行为类型（int 枚举存储）</summary>
public enum ActivityAction
{
    // ===== 登录 / 会话 =====
    Login = 1,          // 登录成功
    LoginFailed = 2,    // 登录失败（密码错 / 账号不存在，Detail.reason 区分）
    LoginDenied = 3,    // 登录被拒（账号被禁用）
    Logout = 4,         // 登出

    // ===== 笔记 =====
    NoteCreate = 10,
    NoteUpdate = 11,
    NoteDelete = 12,
    NoteRestore = 13,   // 版本恢复
    NotePin = 14,
    NoteUnpin = 15,
    NoteView = 16,      // 浏览详情

    // ===== 附件 =====
    AttachmentUpload = 20,
    AttachmentDelete = 21,

    // ===== 分类 =====
    CategoryCreate = 30,
    CategoryUpdate = 31,
    CategoryDelete = 32,

    // ===== 标签 =====
    TagCreate = 40,
    TagDelete = 41,

    // ===== 资料 =====
    ProfileUpdate = 50,

    // ===== 管理员操作（操作者为管理员） =====
    AdminSetStatus = 60,     // 禁用/解封用户
    AdminResetPassword = 61, // 重置密码
    AdminDeleteUser = 62,    // 删除用户
    AdminDeleteNote = 63,    // 删除笔记

    // ===== 其它（默认关，预留） =====
    Search = 70             // 搜索关键词
}

/// <summary>行为关联的实体类型</summary>
public enum ActivityEntity
{
    None = 0,
    Note = 1,
    Category = 2,
    Tag = 3,
    Attachment = 4,
    User = 5,
    Profile = 6
}
