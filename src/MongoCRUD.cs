using System;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PiskvorkyJobsCZ
{
    public class MongoCRUD
    {
        private IMongoDatabase db;

        public MongoCRUD(string connectionString, string database)
        {
            var client = new MongoClient(connectionString);
            db = client.GetDatabase(database);
        }

        public void InsertRecord<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertOne(record);
        }

        public List<T> LoadRecords<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).ToList();
        }

        public async Task<List<T>> LoadRecordsAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return await collection.Find(new BsonDocument()).ToListAsync();
        }

        public T LoadRecord<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).Limit(1).Single();
        }

        public async Task<T> LoadRecordAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return await collection.Find(new BsonDocument()).Limit(1).SingleAsync();
        }

        public long RecordsLength<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.CountDocuments(new BsonDocument());
        }

        public async Task<long> RecordsLengthAsync<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return await collection.CountDocumentsAsync(new BsonDocument());
        }

        public T LoadRecordsById<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("GameToken", id);
            var query = collection.Find(filter);
            if (query.CountDocuments() == 0)
            {
                return default(T);
            }
            else
            {
                return collection.Find(filter).First();
            }
        }

        public void UpsertRecord<T>(string table, string id, T record)
        {
            var collection = db.GetCollection<T>(table);
            var result = collection.ReplaceOne(
                new BsonDocument("_id", id),
                record,
                new UpdateOptions { IsUpsert = true });
        }

        public void DeleteRecord<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);
            var filter = Builders<T>.Filter.Eq("GameToken", id);
            collection.DeleteOne(filter);
        }
    }
}