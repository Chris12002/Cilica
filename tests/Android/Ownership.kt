package dev.cilica

import java.util.concurrent.CompletableFuture
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ExecutionException
import java.util.concurrent.TimeUnit
import dev.cilica.CilicaSessionRuntime as Runtime
import dev.cilica.CilicaSessionRuntime.Value as Value

private fun <T> CompletableFuture<T>.await(): T = get(15, TimeUnit.SECONDS)
private fun fails(reason: String, status: Int = 0, action: () -> CompletableFuture<*>) {
    try { action().await(); error("Expected $reason/$status") }
    catch (error: ExecutionException) {
        val failure = error.cause as Runtime.Failure
        check(failure.reason == reason && failure.status == status) { failure.toString() }
    }
}
private fun Runtime.Outcome.handle() = (value as Value.Reference).handle!!
private fun read(runtime: Runtime, session: Runtime.Handle) =
    (runtime.invoke(3, listOf(Value.Reference(session))).await().value as Value.Record).fields

/** Controlled test synchronization only: applications must never block a worker completion. */
private class WorkerGate(runtime: Runtime) : AutoCloseable {
    private val entered = CountDownLatch(1)
    private val release = CountDownLatch(1)
    private val completion: CompletableFuture<Boolean>
    init {
        val caller = Thread.currentThread()
        var pending: CompletableFuture<Boolean>
        var attempts = 0
        do {
            check(attempts++ < 1000)
            pending = runtime.ready().thenApply {
                if (Thread.currentThread() === caller) false
                else { entered.countDown(); check(release.await(10, TimeUnit.SECONDS)); true }
            }
        } while (pending.isDone && !pending.await())
        completion = pending
        check(entered.await(10, TimeUnit.SECONDS))
    }
    override fun close() { release.countDown(); check(completion.await()) }
}

object Ownership {
    @JvmStatic fun main(args: Array<String>) {
        var cycles = 0
        var stale: Runtime.Handle? = null
        for (stress in listOf(false, true)) repeat(40) {
            val runtime = Runtime(stress, 4)
            try {
                runtime.ready().await()
                stale?.let { previous -> fails("invalidHandle") { runtime.clone(previous) } }
                val label = runtime.importUTF16(charArrayOf('q', '\u0000', '\ud800')).await()
                val session = runtime.invoke(1, listOf(Value.Reference(label))).await().handle()
                val alias = runtime.clone(session).await()
                val spare = runtime.clone(session).await() // All four native handle slots occupied.
                val before = read(runtime, session)
                fails("native", 5) { runtime.invoke(2, listOf(Value.Reference(session), Value.Int32(7)), true) }
                check(read(runtime, session) == before) // Capture quota failure must precede mutation.
                fails("native", 5) { runtime.importUTF16(charArrayOf('x')) }
                runtime.release(spare).await()
                val fault = runtime.invoke(2, listOf(Value.Reference(session), Value.Int32(-1)), true).await()
                check(fault.fault != null)
                val exception = fault.exception!!
                check(runtime.exceptionDetails(exception).await().message == "negative delta")
                runtime.release(exception).await()
                WorkerGate(runtime).use {
                    val discarded = runtime.clone(session)
                    check(discarded.cancel(true))
                }
                runtime.ready().await() // Discarded clone still executed and consumed the fourth slot.
                fails("native", 5) { runtime.clone(session) }
                runtime.release(alias).await()
                val recovered = runtime.clone(session).await()
                runtime.release(recovered).await()
                check(runtime.copyUTF16(label).await().contentEquals(charArrayOf('q', '\u0000', '\ud800')))
                WorkerGate(runtime).use {
                    val discardedPause = runtime.pause()
                    check(discardedPause.cancel(true))
                }
                check(runtime.lifecycle().await() == "paused")
                fails("paused") { runtime.invoke(3, listOf(Value.Reference(session))) }
                WorkerGate(runtime).use {
                    val closeObservation = runtime.close()
                    check(closeObservation.cancel(true))
                    fails("closed") { runtime.ready() }
                }
                runtime.close().await() // Cancelled close observation cannot cancel native cleanup.
                stale = session
                cycles++
            } finally { runtime.close().await() }
        }
        var registryRecoveries = 0
        repeat(2) {
            val owners = ArrayList<Runtime>()
            var overflow: Runtime? = null
            var replacement: Runtime? = null
            try {
                repeat(64) {
                    val owner = Runtime(); owners.add(owner); owner.ready().await()
                }
                val failed = Runtime(); overflow = failed
                fails("native", 5) { failed.ready() }
                fails("native", 5) { failed.importUTF16(charArrayOf('x')) }
                failed.close().await(); failed.close().await()
                owners.removeAt(0).close().await()
                val fresh = Runtime(); replacement = fresh; fresh.ready().await()
                val text = fresh.importUTF16(charArrayOf('r')).await()
                check(fresh.copyUTF16(text).await().contentEquals(charArrayOf('r')))
                val survivor = owners.last()
                val existing = survivor.importUTF16(charArrayOf('s')).await()
                check(survivor.copyUTF16(existing).await().contentEquals(charArrayOf('s')))
                registryRecoveries++
            } finally {
                overflow?.close()?.await(); replacement?.close()?.await()
                owners.forEach { it.close().await() }
            }
        }
        // Futures resolve just before their worker exits; wait for that bounded final transition.
        val deadline = System.nanoTime()+TimeUnit.SECONDS.toNanos(5)
        while (Thread.getAllStackTraces().keys.any { it.isAlive && it.name == "CilicaSession-runtime" }) {
            check(System.nanoTime() < deadline) { "Runtime worker survived close" }
            Thread.sleep(10)
        }
        println("PASS Kotlin ownership cycles=$cycles registry_recoveries=$registryRecoveries cancelled_leases=80 cancelled_pauses=80 cancelled_close_views=80 workers_remaining=0")
    }
}
