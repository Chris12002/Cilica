// Authored native reductions. Included after the pinned generated closure.
static unsigned long long checks = 0;
#define REQUIRE(value) do { ++checks; if (!(value)) { std::fprintf(stderr,"check failed line %d: %s\n",__LINE__,#value); std::abort(); } } while (false)

[[gnu::noinline]] static void topology_cases() {
    Instance heap; Context context(heap); Object* nodes = new Object[8193]{}; Object foreign{};
    for (int n : {1,2,17,257,8192,8193}) {
        for (int i=0;i<n;++i) nodes[i].next = i+1<n ? &nodes[i+1] : nullptr;
        heap.head=nodes; heap.count=std::size_t(n);
        for (std::size_t budget : {std::size_t(0),std::size_t(1),sizeof(void*),3*sizeof(void*),std::size_t(65536),std::numeric_limits<std::size_t>::max()}) {
            heap.ownership_limit=budget;
            for (bool failure : {false,true}) {
                heap.release_ownership_scratch(); heap.ownership_fail_allocation=failure;
                {
                    OwnershipBatch batch(heap);
                    REQUIRE(batch.contains(nullptr));
                    for (int i=0;i<n;++i) REQUIRE(batch.contains(&nodes[i]));
                    REQUIRE(batch.contains(&nodes[0])); REQUIRE(!batch.contains(&foreign));
                    REQUIRE(!batch.contains(reinterpret_cast<Object*>(std::uintptr_t{1})));
                    REQUIRE(batch.contains(&nodes[n-1]));
                    REQUIRE(heap.ownership_bytes==heap.ownership_capacity*sizeof(void*));
                    REQUIRE(heap.ownership_bytes<=65536 && heap.ownership_bytes<=budget);
                }
                REQUIRE(!heap.ownership_active);
            }
        }
    }
    // Every reachable position and absent roots in self/two/long cycles;
    // restart each query to exercise frontier transitions, not just cache hits.
    for(int n : {1,2,17,65}) {
        for(int i=0;i<n;++i)nodes[i].next=&nodes[(i+1)%n];
        heap.head=nodes;heap.count=std::size_t(n);
        for(std::size_t budget : {std::size_t(0),sizeof(void*),2*sizeof(void*),std::size_t(65536)}) {
            heap.ownership_limit=budget;
            for(bool failure : {false,true}) {
                heap.release_ownership_scratch();heap.ownership_fail_allocation=failure;
                for(int i=0;i<n;++i) {
                    OwnershipBatch batch(heap);
                    REQUIRE(batch.contains(&nodes[i]));REQUIRE(batch.contains(nullptr));
                    REQUIRE(batch.contains(&nodes[i]));REQUIRE(batch.contains(&nodes[0]));
                    REQUIRE(!batch.contains(&foreign));
                    REQUIRE(batch.contains(&nodes[i]));
                }
            }
        }
    }
    heap.head=nullptr;heap.count=0;delete[] nodes;
}

[[gnu::noinline]] static void unreadable_tail(bool missing) {
    Instance heap; Context context(heap); Object node{};
    node.next=reinterpret_cast<Object*>(std::uintptr_t{1});heap.head=&node;heap.count=1;
    Object* root=&node;Frame frame;RootScope scope(context,frame,&root,1);
    REQUIRE(valid_roots(context)); REQUIRE(valid_roots(context));
    root=nullptr;REQUIRE(valid_roots(context));
    if(missing) { Object foreign{};root=&foreign;(void)valid_roots(context);std::abort(); }
    heap.head=nullptr;heap.count=0;
}

static void nested_prepare(Instance& heap) noexcept {
    REQUIRE(heap.ownership_active); auto* buffer=heap.ownership_scratch;
    { OwnershipBatch nested(heap); REQUIRE(!nested.exclusive); REQUIRE(nested.contains(heap.head)); }
    REQUIRE(heap.ownership_active && heap.ownership_scratch==buffer);
    heap.ownership_limit=sizeof(void*); // Must be rechecked before new allocation.
}

[[gnu::noinline]] static void budget_cases() {
    Instance heap;Context context(heap);Object node{},foreign{};heap.head=&node;heap.count=4096;
    heap.ownership_allocate_hook=nested_prepare;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(&node));REQUIRE(heap.ownership_bytes==sizeof(void*)); }
    heap.ownership_allocate_hook=nullptr;
    heap.ownership_limit=65536;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(&node));REQUIRE(heap.ownership_bytes==65536);
      auto* buffer=heap.ownership_scratch;
      { OwnershipBatch nested(heap);REQUIRE(!nested.exclusive);REQUIRE(!nested.contains(&foreign)); }
      REQUIRE(heap.ownership_active && heap.ownership_scratch==buffer);REQUIRE(batch.contains(&node)); }
    REQUIRE(heap.ownership_peak==65536);
    heap.ownership_limit=0;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(&node));REQUIRE(heap.ownership_bytes==0); }
    heap.ownership_limit=65536;heap.ownership_fail_allocation=true;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(&node));REQUIRE(heap.ownership_capacity==0); }
    heap.ownership_fail_allocation=false;
    heap.count=1;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(&node));REQUIRE(heap.ownership_bytes==2*sizeof(void*)); }
    Object* root=&foreign;Frame frame;
    { RootScope scope(context,frame,&root,1);REQUIRE(!valid_roots(context));REQUIRE(!heap.ownership_active); }
    heap.head=nullptr;heap.count=0;
    { OwnershipBatch batch(heap);REQUIRE(batch.contains(nullptr));REQUIRE(heap.ownership_bytes==0); }
    // Metadata-only charges have no following object allocation to hide a missed peak.
    heap.total_memory_peak=0;
    REQUIRE(ensure_static_storage(context));REQUIRE(heap.total_memory_peak==heap.total_memory());
    REQUIRE(ensure_initialization_storage(context));REQUIRE(heap.total_memory_peak==heap.total_memory());
    REQUIRE(heap.limit==1048576 && heap.object_limit==4096);
    std::printf("{\"instance_size\":%zu,\"batch_size\":%zu,\"scratch_peak\":%zu,\"metadata_peak\":%zu}\n",sizeof(Instance),sizeof(OwnershipBatch),heap.ownership_peak,heap.total_memory_peak);
}

[[gnu::noinline]] static void runtime_cases() {
    Instance heap;Context c(heap);REQUIRE(call(c,0,1)==6146);
    auto count=heap.count, bytes=heap.bytes;Object* orphan=heap.head;Object* saved=orphan->next;
    heap.head=saved;--heap.count;heap.bytes-=orphan->allocation_bytes;
    {Frame frame;RootScope root(c,frame,&orphan,1);
     REQUIRE(!valid_roots(c));auto report=mark_reachable(c);REQUIRE(report.status==MarkStatus::invalid);
     REQUIRE(!orphan->marked && orphan->mark_next==nullptr);REQUIRE(!heap.ownership_active);}
    heap.head=orphan;heap.count=count;heap.bytes=bytes;
    REQUIRE(mark_reachable(c).status==MarkStatus::complete);
    // Invalid inventories remain atomic; restore and retry succeeds.
    auto scratch=heap.ownership_scratch;auto charged=heap.ownership_bytes;
    for(int fault=0;fault<4;++fault) {
        if(fault==0)++heap.count;
        if(fault==1)++heap.bytes;
        if(fault==2)orphan->next=orphan;
        if(fault==3)orphan->owner=nullptr;
        auto report=collect(c);REQUIRE(report.status==MarkStatus::invalid);
        REQUIRE(heap.head==orphan && !orphan->marked && orphan->mark_next==nullptr);
        REQUIRE(heap.ownership_scratch==scratch && heap.ownership_bytes==charged);
        heap.count=count;heap.bytes=bytes;orphan->next=saved;orphan->owner=&heap;
        REQUIRE(mark_reachable(c).status==MarkStatus::complete);
    }
    REQUIRE(collect(c,MarkLimits{0,0}).status==MarkStatus::limit);
    REQUIRE(heap.ownership_scratch==scratch);
    heap.marking=true;REQUIRE(collect(c).status==MarkStatus::busy);heap.marking=false;
    REQUIRE(heap.ownership_scratch==scratch);
    REQUIRE(collect(c).status==MarkStatus::complete);
    REQUIRE(heap.count==8 && heap.bytes==5488 && heap.ownership_bytes==0);
    REQUIRE(valid_roots(c));REQUIRE(heap.ownership_bytes!=0);
    // Zero-reclaim collection still releases scratch.
    REQUIRE(collect(c).status==MarkStatus::complete);REQUIRE(heap.count==8 && heap.ownership_bytes==0);
    Instance other;Context second(other);Object* foreign=heap.head;Frame frame;
    { RootScope scope(second,frame,&foreign,1);REQUIRE(!valid_roots(second));REQUIRE(!other.ownership_active); }
    REQUIRE(call(c,0,1)==6146);REQUIRE(heap.total_memory_peak>=heap.total_memory());
}

[[gnu::noinline]] static int prefix_cases(const char* mode) {
    if(std::strcmp(mode,"bad-tail")==0) { unreadable_tail(true);return 1; }
    if(std::strcmp(mode,"tail")==0) { unreadable_tail(false);return 0; }
    topology_cases();unreadable_tail(false);budget_cases();runtime_cases();
    std::printf("{\"status\":\"passed\",\"checks\":%llu}\n",checks);return 0;
}
