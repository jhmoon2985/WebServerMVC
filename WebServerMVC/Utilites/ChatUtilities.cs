// Utilities/ChatUtilities.cs
namespace WebServerMVC.Utilities
{
    public static class ChatUtilities
    {
        public static string CreateChatGroupName(string clientId1, string clientId2)
        {
            // 항상 정렬된 ID를 사용하여 일관된 그룹 이름 생성
            string[] sortedIds = new[] { clientId1, clientId2 }.OrderBy(id => id).ToArray();
            return $"chat_{sortedIds[0]}_{sortedIds[1]}";
        }
    }
}