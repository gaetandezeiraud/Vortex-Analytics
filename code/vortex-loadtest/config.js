export default {
  apiBase: "http://localhost:9876",
  tenants: ["alpha"],

  users: 500,           // number of fake users
  eventsPerUser: 50,    // number of events per user
  concurrency: 50,      // number of users hitting API in parallel
  batchMode: true,      // true = /batch endpoint ; false = /track
  batchSize: 15         // same batching size as backend
};
