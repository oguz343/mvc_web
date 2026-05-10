using Google.Cloud.Firestore;

namespace mvc_web.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;

    public FirestoreService(FirestoreDb db)
    {
        _db = db;
    }

    public FirestoreDb Db => _db;

    public CollectionReference Users => _db.Collection("users");
    public CollectionReference Classes => _db.Collection("classes");
    public CollectionReference Lessons => _db.Collection("lessons");
    public CollectionReference Assignments => _db.Collection("assignments");
    public CollectionReference Submissions => _db.Collection("submissions");
    public CollectionReference Announcements => _db.Collection("announcements");
    public CollectionReference PasswordRequests => _db.Collection("password_requests");

    public async Task<int> CountAsync(CollectionReference collection)
    {
        var snapshot = await collection.GetSnapshotAsync();
        return snapshot.Count;
    }

    public async Task<int> CountWhereAsync(
        CollectionReference collection,
        string field,
        object value
    )
    {
        var snapshot = await collection
            .WhereEqualTo(field, value)
            .GetSnapshotAsync();

        return snapshot.Count;
    }

    public static string GetString(
        DocumentSnapshot document,
        string fieldName,
        string defaultValue = ""
    )
    {
        if (!document.Exists)
        {
            return defaultValue;
        }

        var data = document.ToDictionary();

        if (!data.ContainsKey(fieldName) || data[fieldName] == null)
        {
            return defaultValue;
        }

        return data[fieldName]?.ToString() ?? defaultValue;
    }

    public static bool GetBool(
        DocumentSnapshot document,
        string fieldName,
        bool defaultValue = false
    )
    {
        if (!document.Exists)
        {
            return defaultValue;
        }

        var data = document.ToDictionary();

        if (!data.ContainsKey(fieldName) || data[fieldName] == null)
        {
            return defaultValue;
        }

        if (data[fieldName] is bool boolValue)
        {
            return boolValue;
        }

        return bool.TryParse(data[fieldName].ToString(), out var result)
            ? result
            : defaultValue;
    }

    public static int GetInt(
        DocumentSnapshot document,
        string fieldName,
        int defaultValue = 0
    )
    {
        if (!document.Exists)
        {
            return defaultValue;
        }

        var data = document.ToDictionary();

        if (!data.ContainsKey(fieldName) || data[fieldName] == null)
        {
            return defaultValue;
        }

        return int.TryParse(data[fieldName].ToString(), out var result)
            ? result
            : defaultValue;
    }
}