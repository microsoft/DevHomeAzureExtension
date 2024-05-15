use std::rc::Rc;
use std::cell::RefCell;

#[derive(Debug)]
struct Node<T> {
    data: T,
    next: Option<Rc<RefCell<Node<T>>>>,
}

impl<T> Node<T> {
    fn new(data: T) -> Self {
        Node {
            data,
            next: None,
        }
    }
}

#[derive(Debug)]
struct SinglyLinkedList<T> {
    head: Option<Rc<RefCell<Node<T>>>>,
    tail: Option<Rc<RefCell<Node<T>>>>,
    length: usize,
}

impl<T> SinglyLinkedList<T> {
    fn new() -> Self {
        SinglyLinkedList {
            head: None,
            tail: None,
            length: 0,
        }
    }

    fn push(&mut self, data: T) {
        let new_node = Rc::new(RefCell::new(Node::new(data)));

        match self.tail.take() {
            Some(old_tail) => {
                old_tail.borrow_mut().next = Some(new_node.clone());
            }
            None => {
                self.head = Some(new_node.clone());
            }
        }

        self.tail = Some(new_node);
        self.length += 1;
    }
}

fn main() {
    let mut list = SinglyLinkedList::new();
    list.push(1);
    list.push(2);
    list.push(3);
    println!("{:?}", list);
}
