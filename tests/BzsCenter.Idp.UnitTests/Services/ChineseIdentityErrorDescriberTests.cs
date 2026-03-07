using BzsCenter.Idp.Services;

namespace BzsCenter.Idp.UnitTests.Services;

public class ChineseIdentityErrorDescriberTests
{
    [Theory]
    [InlineData("test-user", "用户名 'test-user' 已存在")]
    [InlineData("张三", "用户名 '张三' 已存在")]
    public void DuplicateUserName_ReturnsExpectedChineseMessage(string userName, string expectedDescription)
    {
        var describer = new ChineseIdentityErrorDescriber();

        var error = describer.DuplicateUserName(userName);

        Assert.Equal("DuplicateUserName", error.Code);
        Assert.Equal(expectedDescription, error.Description);
    }

    [Fact]
    public void PasswordTooShort_ReturnsExpectedChineseMessageWithLength()
    {
        var describer = new ChineseIdentityErrorDescriber();

        var error = describer.PasswordTooShort(8);

        Assert.Equal("PasswordTooShort", error.Code);
        Assert.Equal("密码太短，至少需要 8 个字符", error.Description);
    }
}
